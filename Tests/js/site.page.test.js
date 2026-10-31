import { afterEach, describe, expect, it, vi } from "vitest";

async function run(html, stored) {
    document.body.innerHTML = html;
    localStorage.clear();
    if (stored) localStorage.setItem("gm-theme", stored);
    vi.resetModules();
    await import("../../wwwroot/js/site.js");
}

afterEach(() => {
    vi.unstubAllGlobals();
});

function $(selector) {
    return document.querySelector(selector);
}

const THEME = `
<nav class="gm-nav navbar"></nav>
<select id="gmTheme">
  <option value="light">Light</option><option value="dark">Dark</option><option value="cyberpunk">Cyberpunk</option>
  <option value="lovecraftian">Lovecraftian</option><option value="fantasy">Fantasy</option>
</select>`;

describe("theme picker", () => {
    it("applies the saved theme to the page, the navbar and the picker", async () => {
        await run(THEME, "cyberpunk");
        expect(document.documentElement.getAttribute("data-gm-theme")).toBe("cyberpunk");
        expect(document.documentElement.getAttribute("data-bs-theme")).toBe("dark");
        expect($(".gm-nav").classList.contains("navbar-dark")).toBe(true);
        expect($("#gmTheme").value).toBe("cyberpunk");
    });

    it("switches to light and remembers a pick", async () => {
        await run(THEME, "light");
        expect(document.documentElement.getAttribute("data-bs-theme")).toBe("light");
        expect($(".gm-nav").classList.contains("navbar-light")).toBe(true);
        $("#gmTheme").value = "fantasy";
        $("#gmTheme").dispatchEvent(new Event("change"));
        expect(localStorage.getItem("gm-theme")).toBe("fantasy");
        expect(document.documentElement.getAttribute("data-gm-theme")).toBe("fantasy");
    });

    it("falls back to dark for an unknown theme, saved or picked", async () => {
        await run(THEME, "neon");
        expect(document.documentElement.getAttribute("data-gm-theme")).toBe("dark");
        $("#gmTheme").value = "";
        $("#gmTheme").dispatchEvent(new Event("change"));
        expect($("#gmTheme").value).toBe("dark");
    });

    it("still themes the page when storage is blocked, with no navbar or picker", async () => {
        vi.stubGlobal("localStorage", {
            getItem() { throw new Error("blocked"); },
            setItem() { throw new Error("blocked"); },
            clear() {},
        });
        await run("");
        expect(document.documentElement.getAttribute("data-gm-theme")).toBe("dark");
    });
});

describe("oracle form", () => {
    const FORM = `
<select id="OracleSubtype"><option>Dice</option><option>Cards</option></select>
<div id="diceFields"></div>
<select id="dicePreset"><option value="custom">Custom</option><option value="d6">d6</option><option value="d20">d20</option></select>
<input id="DiceCount" value="2" /><input id="DiceFaces" value="6" />`;

    it("shows the dice fields only for a dice oracle", async () => {
        await run(FORM);
        expect($("#diceFields").hidden).toBe(false);
        $("#OracleSubtype").value = "Cards";
        $("#OracleSubtype").dispatchEvent(new Event("change"));
        expect($("#diceFields").hidden).toBe(true);
    });

    it("fills the fields from a preset, and moves the preset to match the fields", async () => {
        await run(FORM);
        $("#dicePreset").value = "d20";
        $("#dicePreset").dispatchEvent(new Event("change"));
        expect([$("#DiceCount").value, $("#DiceFaces").value]).toEqual(["1", "20"]);

        $("#dicePreset").value = "custom";
        $("#dicePreset").dispatchEvent(new Event("change"));
        expect([$("#DiceCount").value, $("#DiceFaces").value]).toEqual(["1", "20"]);

        $("#DiceFaces").value = "6";
        $("#DiceFaces").dispatchEvent(new Event("input"));
        expect($("#dicePreset").value).toBe("d6");
        $("#DiceCount").value = "3";
        $("#DiceCount").dispatchEvent(new Event("input"));
        expect($("#dicePreset").value).toBe("custom");
    });

    it("leaves the number fields alone when there is no preset to match", async () => {
        await run(`<select id="OracleSubtype"><option>Dice</option></select><div id="diceFields"></div>
            <input id="DiceCount" value="1" /><input id="DiceFaces" value="6" />`);
        $("#DiceFaces").value = "8";
        $("#DiceFaces").dispatchEvent(new Event("input"));
        expect($("#DiceFaces").value).toBe("8");
    });

    it("works without the preset and number fields", async () => {
        await run(`<select id="OracleSubtype"><option>Cards</option></select><div id="diceFields"></div>`);
        expect($("#diceFields").hidden).toBe(true);
    });
});

describe("entry pager", () => {
    function row(starts, suit, rank) {
        const start = starts === null ? "" : ` data-starts-at="${starts}"`;
        return `<div data-entry-row${start} data-suit="${suit}" data-rank-kind="${rank}">
            <input name="Entries[${starts}].StartsAt" value="${starts ?? ""}" /></div>`;
    }
    const PAGER = (size = ' data-page-size="2"', nav = true) => `
<div data-outcome-pager${size}>
  <button data-filter="decade" data-value="all">All</button><button data-filter="decade" data-value="11-20">11-20</button><button data-filter="decade" data-value="1-20">1-20</button>
  <button data-filter="suit" data-value="all">All</button><button data-filter="suit" data-value="Hearts">Hearts</button>
  <button data-filter="rank" data-value="all">All</button><button data-filter="rank" data-value="face">Face</button>
  ${row(1, "Hearts", "face")}${row(12, "Spades", "number")}${row(15, "Hearts", "number")}${row(null, "Hearts", "face")}
  ${nav ? '<nav data-outcome-pager-nav><button data-outcome-prev></button><span data-outcome-page-info></span><button data-outcome-next></button></nav>' : ""}
</div>`;

    const shown = () => [...document.querySelectorAll("[data-entry-row]")]
        .filter((r) => !r.classList.contains("d-none")).map((r) => r.getAttribute("data-starts-at"));

    it("shows a page of entries at a time and stops at either end", async () => {
        await run(PAGER());
        expect(shown()).toEqual(["1", "12"]);
        expect($("[data-outcome-page-info]").textContent).toBe("Page 1 of 2");
        expect($("[data-outcome-prev]").disabled).toBe(true);
        $("[data-outcome-prev]").click();
        $("[data-outcome-next]").click();
        expect(shown()).toEqual(["15", null]);
        expect($("[data-outcome-next]").disabled).toBe(true);
        $("[data-outcome-next]").disabled = false;
        $("[data-outcome-next]").click();
        expect($("[data-outcome-page-info]").textContent).toBe("Page 2 of 2");
        $("[data-outcome-prev]").click();
        expect(shown()).toEqual(["1", "12"]);
        $("[data-outcome-prev]").disabled = false;
        $("[data-outcome-prev]").click();
        expect($("[data-outcome-page-info]").textContent).toBe("Page 1 of 2");
    });

    it("filters by range, suit and rank, and hides the pager when one page is enough", async () => {
        await run(PAGER());
        $('[data-filter="decade"][data-value="11-20"]').click();
        expect(shown()).toEqual(["12", "15"]);
        expect($("[data-outcome-pager-nav]").hidden).toBe(true);
        expect($('[data-filter="decade"][data-value="11-20"]').getAttribute("aria-pressed")).toBe("true");
        expect($('[data-filter="decade"][data-value="11-20"]').classList.contains("btn-primary")).toBe(true);
        expect($('[data-filter="decade"][data-value="all"]').classList.contains("btn-outline-secondary")).toBe(true);
        $('[data-filter="suit"][data-value="Hearts"]').click();
        expect(shown()).toEqual(["15"]);
        $('[data-filter="decade"][data-value="all"]').click();
        $('[data-filter="rank"][data-value="face"]').click();
        expect(shown()).toEqual(["1", null]);
    });

    it("moves an entry between ranges as its start is edited", async () => {
        await run(PAGER());
        $('[data-filter="decade"][data-value="11-20"]').click();
        const first = $('input[name="Entries[1].StartsAt"]');
        first.value = "18";
        first.dispatchEvent(new Event("input"));
        expect(shown()).toEqual(["18", "12"]);
    });

    it("goes back a page when an edit leaves fewer pages", async () => {
        await run(PAGER());
        $('[data-filter="decade"][data-value="1-20"]').click();
        $("[data-outcome-next]").click();
        expect(shown()).toEqual(["15"]);
        const late = $('input[name="Entries[15].StartsAt"]');
        late.value = "50";
        late.dispatchEvent(new Event("input"));
        expect(shown()).toEqual(["1", "12"]);
    });

    it("works with a pager that has no buttons or page count, and rows without a start", async () => {
        await run(`<div data-outcome-pager data-page-size="1"><div data-entry-row data-suit="Hearts"></div>
            <div data-entry-row data-suit="Spades"></div><nav data-outcome-pager-nav></nav></div>`);
        expect($("[data-outcome-pager-nav]").hidden).toBe(false);
        expect([...document.querySelectorAll("[data-entry-row]")].map((r) => r.classList.contains("d-none"))).toEqual([false, true]);
    });

    it("pages ten at a time by default and works without a pager", async () => {
        await run(PAGER("", false));
        expect(shown()).toEqual(["1", "12", "15", null]);
    });
});

describe("portfolio login links", () => {
    const LINKS = '<a class="portfolio-button" data-portfolio-url="http://portfolio.test">Log in</a><a class="portfolio-button">Log in</a>';

    it("name the portfolio account the browser is logged in to", async () => {
        const fetch = vi.fn(() => Promise.resolve({ ok: true, json: () => Promise.resolve({ username: "Gambler" }) }));
        vi.stubGlobal("fetch", fetch);
        await run(LINKS);
        await vi.waitFor(() => expect($(".portfolio-button").textContent).toBe("Log in as Gambler"));
        expect([...document.querySelectorAll(".portfolio-button")].map((a) => a.textContent)).toEqual(["Log in as Gambler", "Log in as Gambler"]);
        expect(fetch.mock.calls[0][0]).toBe("http://portfolio.test/sso/me");
        expect(fetch.mock.calls[0][1].credentials).toBe("include");
    });

    it("stay as they are when nobody is logged in or the portfolio cannot be reached", async () => {
        for (const reply of [() => Promise.resolve({ ok: false }), () => Promise.reject(new Error("offline")),
            () => Promise.resolve({ ok: true, json: () => Promise.resolve({}) })]) {
            vi.stubGlobal("fetch", vi.fn(reply));
            await run(LINKS);
            await new Promise((resolve) => setTimeout(resolve, 0));
            expect($(".portfolio-button").textContent).toBe("Log in");
        }
    });
});
