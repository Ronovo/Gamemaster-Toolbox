import { afterEach, describe, expect, it, vi } from "vitest";

// The export picker listens on document for gm:list-updated every time site.js runs, so the test that fires it
// comes first, before any other copy of the picker is listening.
async function run(html) {
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

function picked(name) {
    return [...document.querySelectorAll(`input[type=hidden][name="${name}"]`)].map((i) => i.value);
}

function tick(box, on) {
    box.checked = on;
    box.dispatchEvent(new Event("change"));
}

const EXPORT = (flowSelected = '<div id="flow-selected"></div>') => `
<form>
  <input type="radio" name="Scope" id="scopeAll" value="All" checked />
  <input type="radio" name="Scope" id="scopeSelectedOracles" value="SelectedOracles" />
  <input type="radio" name="Scope" id="scopeSelectedFlows" value="SelectedFlows" />
  <fieldset id="oraclePicker">
    <div id="oracleFilters">
      <input name="oq" value="drag" /><input type="hidden" name="returnTo" value="/Export" />
      <select name="tag"><option value="">Any</option></select><input name="off" value="1" disabled /><input value="nameless" />
    </div>
    <a id="selectAllOracles" href="#" data-ids-url="/Export/OracleIds" data-filter-root="#oracleFilters">All</a>
    <a id="selectNoneOracles" href="#" data-ids-url="/Export/OracleIds" data-filter-root="#oracleFilters">None</a>
    <div id="oracle-selected"><input type="hidden" name="SelectedIds" value="3" /></div>
    <div id="oracle-results">
      <input type="checkbox" class="oracle-pick" name="SelectedIds" value="1" />
      <input type="checkbox" class="oracle-pick" name="SelectedIds" value="3" />
    </div>
  </fieldset>
  <fieldset id="flowPicker">
    <a id="selectAllFlows" href="#" data-ids-url="/Export/FlowIds" data-filter-root="#noFilters">All</a>
    ${flowSelected}
    <div id="flow-results"><input type="checkbox" class="flow-pick" name="SelectedFlowIds" value="9" checked /></div>
  </fieldset>
</form>`;

describe("export picker", () => {
    it("keeps ticks in hidden fields, so they survive a new page of results", async () => {
        await run(EXPORT());
        const boxes = () => [...document.querySelectorAll(".oracle-pick")];
        expect(boxes().map((b) => b.checked)).toEqual([false, true]);
        expect(boxes().every((b) => !b.hasAttribute("name"))).toBe(true);
        expect(picked("SelectedFlowIds")).toEqual(["9"]);

        tick(boxes()[0], true);
        tick(boxes()[1], false);
        expect(picked("SelectedIds")).toEqual(["1"]);

        $("#oracle-results").innerHTML = '<input type="checkbox" class="oracle-pick" value="1" /><input type="checkbox" class="oracle-pick" value="4" />';
        $("#oracle-results").dispatchEvent(new CustomEvent("gm:list-updated", { bubbles: true }));
        $("#oracle-results").dispatchEvent(new CustomEvent("gm:list-updated", { bubbles: true }));
        expect(boxes().map((b) => b.checked)).toEqual([true, false]);
        tick(boxes()[1], true);
        expect(picked("SelectedIds")).toEqual(["1", "4"]);
    });

    it("only lets the chosen scope's picker be used", async () => {
        await run(EXPORT());
        expect([$("#oraclePicker").disabled, $("#flowPicker").disabled]).toEqual([true, true]);
        expect($("#oraclePicker").style.opacity).toBe("0.5");
        $("#scopeSelectedOracles").checked = true;
        $("#scopeSelectedOracles").dispatchEvent(new Event("change"));
        expect([$("#oraclePicker").disabled, $("#flowPicker").disabled]).toEqual([false, true]);
        expect($("#oraclePicker").style.opacity).toBe("1");
    });

    function idsServer() {
        const fetch = vi.fn((url) => Promise.resolve({ json: () => Promise.resolve(url.includes("Flow") ? [9, 10] : [1, 3, 5]) }));
        vi.stubGlobal("fetch", fetch);
        $("#oraclePicker").disabled = false;
        $("#flowPicker").disabled = false;
        return fetch;
    }

    it("selects everything the current filters match, not just this page", async () => {
        await run(EXPORT());
        const fetch = idsServer();
        $("#selectAllOracles").click();
        await vi.waitFor(() => expect(picked("SelectedIds")).toEqual(["3", "1", "5"]));
        expect(fetch.mock.calls[0][0]).toBe("http://localhost:3000/Export/OracleIds?oq=drag");
        expect(fetch.mock.calls[0][1].headers).toEqual({ "X-Requested-With": "fetch" });
        expect([...document.querySelectorAll(".oracle-pick")].map((b) => b.checked)).toEqual([true, true]);

        $("#selectAllFlows").click();
        await vi.waitFor(() => expect(picked("SelectedFlowIds")).toEqual(["9", "10"]));
        expect(fetch.mock.calls[1][0]).toBe("http://localhost:3000/Export/FlowIds");
    });

    it("Select none clears everything the filters match, the ticks on this page included", async () => {
        await run(EXPORT());
        idsServer();
        $("#selectAllOracles").click();
        await vi.waitFor(() => expect(picked("SelectedIds")).toHaveLength(3));
        $("#selectNoneOracles").click();
        await new Promise((resolve) => setTimeout(resolve, 20));
        expect(picked("SelectedIds")).toEqual([]);
        expect([...document.querySelectorAll(".oracle-pick")].map((b) => b.checked)).toEqual([false, false]);
    });

    it("Select none keeps what the filters don't match", async () => {
        await run(EXPORT());
        $("#oraclePicker").disabled = false;
        tick(document.querySelectorAll(".oracle-pick")[0], true);
        expect(picked("SelectedIds")).toEqual(["3", "1"]);
        vi.stubGlobal("fetch", vi.fn(() => Promise.resolve({ json: () => Promise.resolve([1, 5]) })));
        $("#selectNoneOracles").click();
        await vi.waitFor(() => expect(picked("SelectedIds")).toEqual(["3"]));
        expect([...document.querySelectorAll(".oracle-pick")].map((b) => b.checked)).toEqual([false, true]);
    });

    it("does nothing without both pickers and both selection boxes", async () => {
        await run(EXPORT(""));
        expect(document.querySelector(".flow-pick").hasAttribute("name")).toBe(true);
        await run('<div id="oraclePicker"></div>');
        expect(document.querySelector("#oraclePicker").disabled).toBeUndefined();
    });
});
