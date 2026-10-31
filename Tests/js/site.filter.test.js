import { afterEach, describe, expect, it, vi } from "vitest";

async function run(html, url = "/Oracles") {
    history.replaceState(null, "", url);
    document.body.innerHTML = html;
    vi.resetModules();
    await import("../../wwwroot/js/site.js");
}

afterEach(() => {
    vi.unstubAllGlobals();
});

function $(selector) {
    return document.querySelector(selector);
}

function serve(pages = {}) {
    const fetch = vi.fn((url) => {
        const reply = pages[new URL(url).search] ?? "<p>results</p>";
        return (typeof reply === "function" ? reply() : Promise.resolve(reply)).then((html) => ({ text: () => Promise.resolve(html) }));
    });
    vi.stubGlobal("fetch", fetch);
    return fetch;
}

function requested(fetch, call = -1) {
    const calls = fetch.mock.calls;
    const url = new URL(calls[call < 0 ? calls.length + call : call][0]);
    return url.pathname + url.search;
}

const FILTER = `
<form data-live-filter action="/Oracles" data-results="#list-results">
  <input type="text" name="q" />
  <select name="sort"><option value="">Any</option><option value="name">Name</option></select>
  <input type="checkbox" name="mine" value="1" /><input type="checkbox" name="shared" value="1" checked />
  <input type="file" name="upload" /><input type="submit" name="go" value="Go" />
  <input name="locked" value="x" disabled /><input value="no name" />
</form>
<div id="list-results"><a data-page href="/Oracles?page=2&q=">2</a></div>`;

describe("live filter", () => {
    it("filters a pause after typing, and keeps the address bar in step", async () => {
        await run(FILTER, "/Oracles?page=3");
        const fetch = serve();
        const listUpdated = vi.fn();
        document.addEventListener("gm:list-updated", listUpdated);
        $('[name="q"]').value = "dragon";
        $('[name="q"]').dispatchEvent(new Event("input"));
        $('[name="q"]').dispatchEvent(new Event("input"));
        expect(fetch).not.toHaveBeenCalled();
        await vi.waitFor(() => expect(fetch).toHaveBeenCalledTimes(1), { timeout: 1000 });
        expect(requested(fetch)).toBe("/Oracles?q=dragon&shared=1&partial=1");
        expect(fetch.mock.calls[0][1].headers).toEqual({ "X-Requested-With": "fetch" });
        await vi.waitFor(() => expect(location.pathname + location.search).toBe("/Oracles?q=dragon&shared=1"));
        expect($("#list-results").innerHTML).toBe("<p>results</p>");
        expect(listUpdated).toHaveBeenCalledTimes(1);
        document.removeEventListener("gm:list-updated", listUpdated);
    });

    it("filters straight away on a dropdown or a submit", async () => {
        await run(FILTER);
        const fetch = serve();
        $('[name="sort"]').value = "name";
        $('[name="sort"]').dispatchEvent(new Event("change"));
        expect(requested(fetch)).toBe("/Oracles?sort=name&shared=1&partial=1");
        await vi.waitFor(() => expect(location.search).toBe("?sort=name&shared=1"));
        $('[name="q"]').value = "storm";
        $("form").dispatchEvent(new Event("submit", { cancelable: true }));
        expect(requested(fetch)).toBe("/Oracles?q=storm&sort=name&shared=1&partial=1");
    });

    it("drops a filter that was cleared from the address bar", async () => {
        await run(FILTER, "/Oracles?q=dragon&sort=name");
        $('[name="q"]').value = "dragon";
        $('[name="sort"]').value = "name";
        const fetch = serve();
        $('[name="q"]').value = "";
        $('[name="sort"]').value = "";
        $('[name="sort"]').dispatchEvent(new Event("change"));
        expect(requested(fetch)).toBe("/Oracles?shared=1&partial=1");
        await vi.waitFor(() => expect(fetch).toHaveBeenCalled());
        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(location.search).toBe("?shared=1");
    });

    it("pages through the results it loaded without losing the filters", async () => {
        await run(FILTER);
        const fetch = serve({ "?page=2&q=&partial=1": '<a data-page href="/Oracles?page=3&q=">3</a>' });
        $("#list-results a[data-page]").click();
        expect(requested(fetch)).toBe("/Oracles?page=2&q=&partial=1");
        await vi.waitFor(() => expect(location.search).toBe("?page=2"));
        $("#list-results a[data-page]").click();
        expect(requested(fetch)).toBe("/Oracles?page=3&q=&partial=1");
    });

    it("on the export page, filters on Enter without submitting the export, using its own page name", async () => {
        await run(`<form method="post" action="/Export/Download">
            <div data-live-filter data-action="/Export/Oracles" data-results="#oracle-results" data-partial="oracles" data-page-param="op">
              <input type="search" name="oq" />
            </div></form><div id="oracle-results"></div>`, "/Export?op=4");
        const fetch = serve();
        const typed = new KeyboardEvent("keydown", { key: "a", bubbles: true, cancelable: true });
        $('[name="oq"]').dispatchEvent(typed);
        expect(typed.defaultPrevented).toBe(false);
        expect(fetch).not.toHaveBeenCalled();
        $('[name="oq"]').value = "storm";
        const enter = new KeyboardEvent("keydown", { key: "Enter", bubbles: true, cancelable: true });
        $('[name="oq"]').dispatchEvent(enter);
        expect(enter.defaultPrevented).toBe(true);
        expect(requested(fetch)).toBe("/Export/Oracles?oq=storm&partial=oracles");
        await vi.waitFor(() => expect(location.pathname + location.search).toBe("/Export?oq=storm"));
    });

    it("falls back to the page's own address, and skips a filter with nowhere to show results", async () => {
        await run(`<div data-live-filter data-results="#r"><select name="tag"><option value="x">x</option></select></div><div id="r"></div>
            <form data-live-filter data-results="#missing"><select name="other"><option>y</option></select></form>`, "/Flows");
        const fetch = serve();
        $('[name="tag"]').dispatchEvent(new Event("change"));
        expect(requested(fetch)).toBe("/Flows?tag=x&partial=1");
        $('[name="other"]').dispatchEvent(new Event("change"));
        expect(fetch).toHaveBeenCalledTimes(1);
    });

    it("uses the default results box when none is named", async () => {
        await run(`<form data-live-filter><select name="tag"><option value="x">x</option></select></form><div id="list-results"></div>`, "/Flows");
        const fetch = serve();
        $('[name="tag"]').dispatchEvent(new Event("change"));
        expect(requested(fetch)).toBe("/Flows?tag=x&partial=1");
        await vi.waitFor(() => expect($("#list-results").innerHTML).toBe("<p>results</p>"));
    });

    it("shows the latest filter's results when an older request answers last", async () => {
        await run(FILTER);
        let answerOld;
        serve({
            "?sort=name&shared=1&partial=1": () => new Promise((resolve) => { answerOld = resolve; }),
            "?shared=1&partial=1": "<p>newest</p>",
        });
        $('[name="sort"]').value = "name";
        $('[name="sort"]').dispatchEvent(new Event("change"));
        $('[name="sort"]').value = "";
        $('[name="sort"]').dispatchEvent(new Event("change"));
        await vi.waitFor(() => expect($("#list-results").innerHTML).toBe("<p>newest</p>"));
        answerOld("<p>older</p>");
        await new Promise((resolve) => setTimeout(resolve, 0));
        expect($("#list-results").innerHTML).toBe("<p>newest</p>");
        expect(location.search).toBe("?shared=1");
    });
});
