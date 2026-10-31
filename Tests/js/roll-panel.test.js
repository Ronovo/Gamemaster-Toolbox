import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

beforeAll(async () => {
    await import("../../wwwroot/js/roll-panel.js");
});

afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
});

const PANEL = `
<div id="panel">
  <div data-roll="result" hidden>
    <span data-roll="label"></span>
    <div data-roll="single"><b data-roll="value"></b><i data-roll="caption"></i><p data-roll="text"></p></div>
    <ol data-roll="list"></ol>
    <span data-roll="reshuffled" hidden>Reshuffled</span>
  </div>
  <div data-roll="error" hidden></div>
</div>`;

const LOG = `
<div id="log" hidden>
  <h3 data-log="heading"></h3>
  <table><tbody data-log="body"></tbody></table>
  <nav data-log="pager" hidden><button data-log="prev"></button><span data-log="info"></span><button data-log="next"></button></nav>
</div>`;

function $(selector) {
    return document.querySelector(selector);
}

function rows() {
    return [...document.querySelectorAll('[data-log="body"] tr')].map((tr) => [...tr.children].map((td) => td.textContent));
}

describe("headline", () => {
    it("shows a dice total, or a card's name", () => {
        expect(window.GmRoll.headline({ isDice: true, value: 17 })).toBe("17");
        expect(window.GmRoll.headline({ isDice: false, key: "Ace of Spades" })).toBe("Ace of Spades");
    });
});

describe("panel", () => {
    it("is nothing without a root", () => {
        expect(window.GmRoll.panel(null)).toBeNull();
    });

    it("shows one outcome with its label, caption and text", () => {
        document.body.innerHTML = PANEL;
        const panel = window.GmRoll.panel($("#panel"));
        panel.showError("old error");
        panel.showOutcome({ isDice: true, value: 12, caption: "2d6", result: "A storm rolls in" }, "Weather");
        expect($('[data-roll="error"]').hidden).toBe(true);
        expect($('[data-roll="result"]').hidden).toBe(false);
        expect($('[data-roll="label"]').textContent).toBe("Weather");
        expect($('[data-roll="label"]').hidden).toBe(false);
        expect($('[data-roll="single"]').hidden).toBe(false);
        expect($('[data-roll="list"]').hidden).toBe(true);
        expect([$('[data-roll="value"]').textContent, $('[data-roll="caption"]').textContent, $('[data-roll="text"]').textContent])
            .toEqual(["12", "2d6", "A storm rolls in"]);
        expect($('[data-roll="text"]').classList.contains("text-muted")).toBe(false);
        expect($('[data-roll="reshuffled"]').hidden).toBe(true);
    });

    it("marks an outcome with no text, and a reshuffled deck", () => {
        document.body.innerHTML = PANEL;
        const panel = window.GmRoll.panel($("#panel"));
        panel.showOutcome({ isDice: false, key: "Queen of Hearts", reshuffled: true });
        expect($('[data-roll="label"]').hidden).toBe(true);
        expect($('[data-roll="caption"]').textContent).toBe("");
        expect($('[data-roll="text"]').textContent).toBe("(no result text)");
        expect($('[data-roll="text"]').classList.contains("text-muted")).toBe(true);
        expect($('[data-roll="reshuffled"]').hidden).toBe(false);
    });

    it("lists several outcomes, whether passed in or carried on the roll", () => {
        document.body.innerHTML = PANEL;
        const panel = window.GmRoll.panel($("#panel"));
        const outcomes = [
            { isDice: true, value: 3, caption: "d6", result: "Low" },
            { isDice: true, value: 6, result: "" },
        ];
        panel.showOutcome({ outcomes }, "Two rolls");
        const items = [...document.querySelectorAll('[data-roll="list"] li')];
        expect(items.map((li) => li.textContent)).toEqual(["3d6Low", "6(no result text)"]);
        expect(items[1].querySelector("div").className).toBe("text-muted");
        expect($('[data-roll="single"]').hidden).toBe(true);

        panel.showOutcome({ isDice: true, value: 1 }, "", outcomes);
        expect(document.querySelectorAll('[data-roll="list"] li')).toHaveLength(2);
    });

    it("works without the optional label, single and list parts", () => {
        document.body.innerHTML = `<div id="bare"><div data-roll="result" hidden><b data-roll="value"></b><i data-roll="caption"></i>
            <p data-roll="text"></p><span data-roll="reshuffled"></span></div><div data-roll="error"></div></div>`;
        const panel = window.GmRoll.panel($("#bare"));
        panel.showOutcome({ isDice: true, value: 4, outcomes: [] });
        expect($('[data-roll="value"]').textContent).toBe("4");
    });

    it("shows an error in place of the result, and clears both", () => {
        document.body.innerHTML = PANEL;
        const panel = window.GmRoll.panel($("#panel"));
        panel.showOutcome({ isDice: true, value: 2 });
        panel.showError("That oracle is empty");
        expect($('[data-roll="result"]').hidden).toBe(true);
        expect($('[data-roll="error"]').textContent).toBe("That oracle is empty");
        expect($('[data-roll="error"]').hidden).toBe(false);
        panel.clear();
        expect($('[data-roll="error"]').hidden).toBe(true);
        expect($('[data-roll="result"]').hidden).toBe(true);
    });
});

describe("log", () => {
    it("is nothing without a wrapper", () => {
        expect(window.GmRoll.log(null)).toBeNull();
    });

    it("keeps the newest roll first, pages by five and keeps fifty by default", () => {
        document.body.innerHTML = LOG;
        const log = window.GmRoll.log($("#log"));
        log.render();
        expect($("#log").hidden).toBe(true);

        for (let i = 1; i <= 52; i++) log.add({ isDice: true, value: i, result: `Roll ${i}`, caption: i === 52 ? "d100" : "" });
        expect($("#log").hidden).toBe(false);
        expect(rows()[0]).toEqual(["52d100", "Roll 52"]);
        expect(rows()).toHaveLength(5);
        expect($('[data-log="heading"]').textContent).toBe("Log (Page 1)");
        expect($('[data-log="info"]').textContent).toBe("Page 1 of 10");
        expect($('[data-log="prev"]').disabled).toBe(true);

        $('[data-log="prev"]').click();
        expect($('[data-log="info"]').textContent).toBe("Page 1 of 10");
        for (let i = 0; i < 10; i++) $('[data-log="next"]').click();
        expect($('[data-log="info"]').textContent).toBe("Page 10 of 10");
        expect($('[data-log="next"]').disabled).toBe(true);
        expect(rows()[4]).toEqual(["3", "Roll 3"]);
        $('[data-log="next"]').disabled = false;
        $('[data-log="next"]').click();
        expect($('[data-log="info"]').textContent).toBe("Page 10 of 10");
        $('[data-log="prev"]').click();
        expect($('[data-log="info"]').textContent).toBe("Page 9 of 10");

        log.add({ isDice: true, value: 99 });
        expect($('[data-log="info"]').textContent).toBe("Page 1 of 10");
        expect(rows()[0]).toEqual(["99", "(no result text)"]);
        expect(document.querySelector('[data-log="body"] td + td').className).toBe("text-muted");

        log.reset();
        expect($("#log").hidden).toBe(true);
    });

    it("works without a heading, and ignores Previous on the first page", () => {
        document.body.innerHTML = LOG.replace('<h3 data-log="heading"></h3>', "");
        const log = window.GmRoll.log($("#log"), { pageSize: 1 });
        log.add({ isDice: true, value: 1 });
        log.add({ isDice: true, value: 2 });
        $('[data-log="prev"]').disabled = false;
        $('[data-log="prev"]').click();
        expect($('[data-log="info"]').textContent).toBe("Page 1 of 2");
    });

    it("reads a flow transcript downwards, follows it to its last page and names each step", () => {
        document.body.innerHTML = LOG;
        const log = window.GmRoll.log($("#log"), { heading: "Run", pageSize: 2, newestFirst: false, showLabel: true, limit: 3 });
        log.add({ label: "Weather", isDice: true, value: 4, result: "Rain" });
        expect($('[data-log="heading"]').textContent).toBe("Run");
        expect($('[data-log="pager"]').hidden).toBe(true);
        log.add({ isDice: false, key: "Ace", result: "Ally" });
        log.add({ label: "Loot", outcomes: [{ isDice: true, value: 1, caption: "d4", result: "Coins" }, { isDice: true, value: 2 }] });
        expect($('[data-log="heading"]').textContent).toBe("Run (Page 2)");
        expect(rows()).toEqual([["Loot", "1d4Coins2(no result text)"]]);
        expect(document.querySelector('[data-log="body"] td[colspan="2"] li:nth-child(2) span').className).toBe("ms-1 text-muted");

        log.add({ label: "Last", isDice: true, value: 9, result: "End" });
        $('[data-log="prev"]').click();
        expect(rows()).toEqual([["", "Ace", "Ally"], ["Loot", "1d4Coins2(no result text)"]]);
    });
});

describe("bind", () => {
    function form(buttons = '<button type="submit" name="go" value="roll">Roll</button>') {
        document.body.innerHTML = `<form id="f" action="/Oracles/Roll/7" method="post">
            <input name="__RequestVerificationToken" value="token" />${buttons}</form>`;
        const element = $("#f");
        element.submit = vi.fn();
        return element;
    }

    function reply(response) {
        const fetch = vi.fn(() => (response instanceof Error ? Promise.reject(response) : Promise.resolve(response)));
        vi.stubGlobal("fetch", fetch);
        return fetch;
    }

    const json = (body, ok = true) => ({ ok, json: () => Promise.resolve(body) });

    it("does nothing without a form", () => {
        expect(window.GmRoll.bind(null, {})).toBeUndefined();
    });

    it("posts the form with the pressed button and hands back the roll", async () => {
        const element = form('<button type="submit" name="mode" value="one">One</button><button type="submit" name="mode" value="all">All</button>');
        const fetch = reply(json({ isDice: true, value: 5 }));
        const onData = vi.fn();
        window.GmRoll.bind(element, { onData });
        const all = element.querySelectorAll("button")[1];
        element.requestSubmit(all);
        expect(all.disabled).toBe(true);
        await vi.waitFor(() => expect(onData).toHaveBeenCalledWith({ isDice: true, value: 5 }));
        await vi.waitFor(() => expect(all.disabled).toBe(false));
        const [url, init] = fetch.mock.calls[0];
        expect(url).toBe("http://localhost:3000/Oracles/Roll/7");
        expect(init.headers).toEqual({ "X-Requested-With": "XMLHttpRequest" });
        expect([...init.body.entries()]).toEqual([["__RequestVerificationToken", "token"], ["mode", "all"]]);
        expect(element.submit).not.toHaveBeenCalled();
    });

    it("reports a refusal the server explains, and ignores a press while a request is out", async () => {
        const element = form();
        const fetch = reply(json({ error: "That deck is empty" }));
        const onError = vi.fn();
        window.GmRoll.bind(element, { onError });
        const button = element.querySelector("button");
        button.disabled = true;
        element.dispatchEvent(new Event("submit", { cancelable: true }));
        expect(fetch).not.toHaveBeenCalled();
        button.disabled = false;
        element.dispatchEvent(new Event("submit", { cancelable: true }));
        await vi.waitFor(() => expect(onError).toHaveBeenCalledWith("That deck is empty"));
    });

    it("falls back to a normal submit on a failed response, a network error or bad JSON", async () => {
        for (const response of [json({}, false), new Error("offline"), { ok: true, json: () => Promise.reject(new SyntaxError("bad")) }]) {
            const element = form();
            reply(response);
            window.GmRoll.bind(element, {});
            element.dispatchEvent(new Event("submit", { cancelable: true }));
            await vi.waitFor(() => expect(element.submit).toHaveBeenCalledTimes(1));
        }
    });

    it("does not post the form again when showing a roll the server already made fails", async () => {
        const element = form();
        const fetch = reply(json({ isDice: false, cards: ["Ace of Spades"] }));
        const failure = new TypeError("the panel is gone");
        const logged = vi.spyOn(console, "error").mockImplementation(() => { });
        window.GmRoll.bind(element, { onData: () => { throw failure; } });
        element.dispatchEvent(new Event("submit", { cancelable: true }));
        await vi.waitFor(() => expect(logged).toHaveBeenCalledWith(failure));
        expect(element.submit).not.toHaveBeenCalled();
        expect(fetch).toHaveBeenCalledTimes(1);
        expect(element.querySelector("button").disabled).toBe(false);
    });

    it("works with no handlers and no submit button", async () => {
        const element = form("");
        reply(json({ isDice: true, value: 1 }));
        window.GmRoll.bind(element, {});
        element.dispatchEvent(new Event("submit", { cancelable: true }));
        reply(json({ error: "nope" }));
        element.dispatchEvent(new Event("submit", { cancelable: true }));
        await new Promise((resolve) => setTimeout(resolve, 0));
        expect(element.submit).not.toHaveBeenCalled();
    });
});
