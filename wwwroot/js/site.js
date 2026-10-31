
// Theme picker: the head script already stamped data-gm-theme so the first paint is right.
// This keeps the select, navbar light/dark classes, and localStorage in step after that.
(function () {
    var KEY = "gm-theme";
    var ALLOWED = { light: true, dark: true, cyberpunk: true, lovecraftian: true, fantasy: true };
    var DEFAULT = "dark";

    function readTheme() {
        var theme = DEFAULT;
        try { theme = localStorage.getItem(KEY) || DEFAULT; } catch (e) { }
        return ALLOWED[theme] ? theme : DEFAULT;
    }

    function applyTheme(theme) {
        if (!ALLOWED[theme]) theme = DEFAULT;

        document.documentElement.setAttribute("data-gm-theme", theme);
        document.documentElement.setAttribute("data-bs-theme", theme === "light" ? "light" : "dark");

        try { localStorage.setItem(KEY, theme); } catch (e) { }

        var nav = document.querySelector(".gm-nav");
        if (nav) {
            nav.classList.toggle("navbar-dark", theme !== "light");
            nav.classList.toggle("navbar-light", theme === "light");
        }

        var picker = document.getElementById("gmTheme");
        if (picker && picker.value !== theme) picker.value = theme;
    }

    applyTheme(readTheme());

    var picker = document.getElementById("gmTheme");
    if (picker) {
        picker.addEventListener("change", function () {
            applyTheme(picker.value);
        });
    }
})();

// Create / Edit oracle form: the "Oracle Type" select chooses dice vs a card deck, and a
// die preset dropdown is a two-way shortcut for the number-of-dice / faces fields. Picking
// a preset fills the fields; editing the fields moves the preset to match (or to Custom).
// The two number fields are the only thing saved for the die.
(function () {
    var kind = document.getElementById("OracleSubtype");
    var diceFields = document.getElementById("diceFields");
    if (!kind || !diceFields) return;

    var preset = document.getElementById("dicePreset");
    var count = document.getElementById("DiceCount");
    var faces = document.getElementById("DiceFaces");

    var standardFaces = { "4": "d4", "6": "d6", "8": "d8", "10": "d10", "12": "d12", "20": "d20", "100": "d100" };

    function syncKind() {
        diceFields.hidden = kind.value !== "Dice";
    }

    // Preset -> fields
    function applyPreset() {
        if (!preset || !count || !faces || preset.value === "custom") return;
        count.value = 1;
        faces.value = parseInt(preset.value.slice(1), 10);
    }

    // Fields -> preset (a single standard die snaps to its option, anything else is Custom)
    function refreshPreset() {
        if (!preset || !count || !faces) return;
        var match = count.value === "1" && standardFaces[faces.value];
        preset.value = match || "custom";
    }

    kind.addEventListener("change", syncKind);
    if (preset) preset.addEventListener("change", applyPreset);
    if (count) count.addEventListener("input", refreshPreset);
    if (faces) faces.addEventListener("input", refreshPreset);

    syncKind();
})();

// List pages and Export pickers: filter as you type / on dropdown change, and page
// without losing the name box. Server-side paging means the query has to go back
// to the server; fetch replaces only the table so the filters stay put.
(function () {
    function readParams(root) {
        var params = new URLSearchParams();
        root.querySelectorAll("input, select, textarea").forEach(function (el) {
            if (!el.name || el.disabled) return;
            if (el.type === "file" || el.type === "submit") return;
            if ((el.type === "checkbox" || el.type === "radio") && !el.checked) return;
            params.set(el.name, el.value);
        });
        return params;
    }

    function actionUrl(root) {
        if (root.tagName === "FORM" && root.getAttribute("action")) return root.action;
        return root.getAttribute("data-action") || window.location.pathname;
    }

    document.querySelectorAll("[data-live-filter]").forEach(function (root) {
        var results = document.querySelector(root.getAttribute("data-results") || "#list-results");
        if (!results) return;

        var timer = null;
        var latest = 0;
        var partial = root.getAttribute("data-partial") || "1";
        var pageParam = root.getAttribute("data-page-param") || "page";

        function bindPager() {
            results.querySelectorAll("a[data-page]").forEach(function (a) {
                a.addEventListener("click", function (e) {
                    e.preventDefault();
                    load(a.href, false);
                });
            });
        }

        function load(url, resetPage) {
            var target = new URL(url, window.location.origin);
            if (resetPage) target.searchParams.delete(pageParam);
            target.searchParams.set("partial", partial);
            // Only the latest request's answer is shown, whatever order they come back in.
            var request = ++latest;

            fetch(target.toString(), { headers: { "X-Requested-With": "fetch" } })
                .then(function (r) { return r.text(); })
                .then(function (html) {
                    if (request !== latest) return;
                    results.innerHTML = html;
                    bindPager();
                    results.dispatchEvent(new CustomEvent("gm:list-updated", { bubbles: true }));

                    var shown = new URL(window.location.href);
                    readParams(root).forEach(function (v, k) {
                        if (!v) shown.searchParams.delete(k);
                    });
                    target.searchParams.forEach(function (v, k) {
                        if (k === "partial") return;
                        if (v) shown.searchParams.set(k, v);
                        else shown.searchParams.delete(k);
                    });
                    shown.searchParams.delete("partial");
                    if (resetPage) shown.searchParams.delete(pageParam);
                    history.replaceState(null, "", shown.pathname + shown.search);
                });
        }

        function filterChanged() {
            var url = new URL(actionUrl(root), window.location.origin);
            readParams(root).forEach(function (v, k) {
                if (v) url.searchParams.set(k, v);
                else url.searchParams.delete(k);
            });
            load(url.toString(), true);
        }

        if (root.tagName === "FORM") {
            root.addEventListener("submit", function (e) {
                e.preventDefault();
                filterChanged();
            });
        } else {
            // Filters sit inside the Export POST form; Enter in a name box must not
            // download a file.
            root.addEventListener("keydown", function (e) {
                if (e.key === "Enter") {
                    e.preventDefault();
                    filterChanged();
                }
            });
        }

        root.querySelectorAll("input[type=text], input[type=search]").forEach(function (input) {
            input.addEventListener("input", function () {
                clearTimeout(timer);
                timer = setTimeout(filterChanged, 300);
            });
        });

        root.querySelectorAll("select").forEach(function (select) {
            select.addEventListener("change", filterChanged);
        });

        bindPager();
    });
})();

// Export picker: hidden SelectedIds / SelectedFlowIds are the source of truth so a
// tick survives paging and filter changes. Select all / none apply to the current
// filter set, not just the visible page.
(function () {
    var oraclePicker = document.getElementById("oraclePicker");
    var flowPicker = document.getElementById("flowPicker");
    if (!oraclePicker || !flowPicker) return;

    function selection(containerId, checkboxSelector, inputName) {
        var box = document.getElementById(containerId);
        if (!box) return null;

        function has(id) {
            return !!box.querySelector('input[name="' + inputName + '"][value="' + id + '"]');
        }

        function add(id) {
            id = String(id);
            if (has(id)) return;
            var input = document.createElement("input");
            input.type = "hidden";
            input.name = inputName;
            input.value = id;
            box.appendChild(input);
        }

        function remove(id) {
            var el = box.querySelector('input[name="' + inputName + '"][value="' + id + '"]');
            if (el) el.remove();
        }

        function syncVisible() {
            document.querySelectorAll(checkboxSelector).forEach(function (cb) {
                if (cb.checked) add(cb.value);
                cb.checked = has(cb.value);
                cb.removeAttribute("name");
            });
        }

        return {
            add: add,
            remove: remove,
            addAll: function (ids) { ids.forEach(add); },
            removeAll: function (ids) {
                ids.forEach(remove);
                // syncVisible adopts ticked boxes, so the removed ones are unticked first.
                var removed = ids.map(String);
                document.querySelectorAll(checkboxSelector).forEach(function (cb) {
                    if (removed.indexOf(cb.value) !== -1) cb.checked = false;
                });
            },
            syncVisible: syncVisible
        };
    }

    var oracles = selection("oracle-selected", ".oracle-pick", "SelectedIds");
    var flows = selection("flow-selected", ".flow-pick", "SelectedFlowIds");
    if (!oracles || !flows) return;

    function bindChecks(selector, set) {
        document.querySelectorAll(selector).forEach(function (cb) {
            if (cb.dataset.gmBound === "1") return;
            cb.dataset.gmBound = "1";
            cb.addEventListener("change", function () {
                if (cb.checked) set.add(cb.value);
                else set.remove(cb.value);
            });
        });
        set.syncVisible();
    }

    function bindAll() {
        bindChecks(".oracle-pick", oracles);
        bindChecks(".flow-pick", flows);
    }

    bindAll();
    document.addEventListener("gm:list-updated", bindAll);

    var selectedOracles = document.getElementById("scopeSelectedOracles");
    var selectedFlows = document.getElementById("scopeSelectedFlows");

    function setEnabled(box, on) {
        box.disabled = !on;
        box.style.opacity = on ? "1" : "0.5";
    }

    function sync() {
        setEnabled(oraclePicker, selectedOracles.checked);
        setEnabled(flowPicker, selectedFlows.checked);
    }

    document.getElementsByName("Scope").forEach(function (radio) {
        radio.addEventListener("change", sync);
    });
    sync();

    function readFilterParams(root) {
        var params = new URLSearchParams();
        if (!root) return params;
        root.querySelectorAll("input, select").forEach(function (el) {
            if (!el.name || el.disabled) return;
            if (el.type === "hidden" && el.name === "returnTo") return;
            if (el.value) params.set(el.name, el.value);
        });
        return params;
    }

    function bindSelectLink(link, set, add) {
        if (!link) return;
        link.addEventListener("click", function (e) {
            e.preventDefault();
            var url = new URL(link.getAttribute("data-ids-url"), window.location.origin);
            var params = readFilterParams(document.querySelector(link.getAttribute("data-filter-root")));
            params.forEach(function (v, k) { url.searchParams.set(k, v); });
            fetch(url.toString(), { headers: { "X-Requested-With": "fetch" } })
                .then(function (r) { return r.json(); })
                .then(function (ids) {
                    if (add) set.addAll(ids);
                    else set.removeAll(ids);
                    set.syncVisible();
                });
        });
    }

    bindSelectLink(document.getElementById("selectAllOracles"), oracles, true);
    bindSelectLink(document.getElementById("selectNoneOracles"), oracles, false);
    bindSelectLink(document.getElementById("selectAllFlows"), flows, true);
    bindSelectLink(document.getElementById("selectNoneFlows"), flows, false);
})();

// Edit Entries and Details: jump links filter the visible set; the rest are
// hidden 10 (dice) or 15 (cards) at a time. Edit Entries keeps every row in the
// form so Save still posts the full list.
(function () {
    var root = document.querySelector("[data-outcome-pager]");
    if (!root) return;

    var pageSize = parseInt(root.getAttribute("data-page-size"), 10) || 10;
    var rows = Array.prototype.slice.call(root.querySelectorAll("[data-entry-row]"));
    var pager = root.querySelector("[data-outcome-pager-nav]");
    var prev = root.querySelector("[data-outcome-prev]");
    var next = root.querySelector("[data-outcome-next]");
    var info = root.querySelector("[data-outcome-page-info]");

    var filters = { decade: "all", suit: "all", rank: "all" };
    var page = 1;

    function matches(row) {
        if (filters.decade !== "all") {
            var starts = parseInt(row.getAttribute("data-starts-at"), 10);
            var parts = filters.decade.split("-");
            var from = parseInt(parts[0], 10);
            var to = parseInt(parts[1], 10);
            if (isNaN(starts) || starts < from || starts > to) return false;
        }
        if (filters.suit !== "all" && row.getAttribute("data-suit") !== filters.suit) return false;
        if (filters.rank !== "all" && row.getAttribute("data-rank-kind") !== filters.rank) return false;
        return true;
    }

    function render() {
        var matched = rows.filter(matches);
        var totalPages = Math.max(1, Math.ceil(matched.length / pageSize));
        if (page > totalPages) page = totalPages;

        var start = (page - 1) * pageSize;
        var visible = matched.slice(start, start + pageSize);

        rows.forEach(function (row) {
            row.classList.toggle("d-none", visible.indexOf(row) === -1);
        });

        if (!pager) return;
        if (matched.length <= pageSize) {
            pager.hidden = true;
            return;
        }

        pager.hidden = false;
        if (info) info.textContent = "Page " + page + " of " + totalPages;
        if (prev) prev.disabled = page === 1;
        if (next) next.disabled = page === totalPages;
    }

    function setFilter(kind, value) {
        filters[kind] = value;
        page = 1;
        root.querySelectorAll('[data-filter="' + kind + '"]').forEach(function (btn) {
            var on = btn.getAttribute("data-value") === value;
            btn.classList.toggle("btn-primary", on);
            btn.classList.toggle("btn-outline-secondary", !on);
            btn.setAttribute("aria-pressed", on ? "true" : "false");
        });
        render();
    }

    root.querySelectorAll("[data-filter]").forEach(function (btn) {
        btn.addEventListener("click", function () {
            setFilter(btn.getAttribute("data-filter"), btn.getAttribute("data-value"));
        });
    });

    rows.forEach(function (row) {
        var startInput = row.querySelector("input[name$='.StartsAt']");
        if (!startInput) return;
        startInput.addEventListener("input", function () {
            row.setAttribute("data-starts-at", startInput.value);
            render();
        });
    });

    if (prev) prev.addEventListener("click", function () { if (page > 1) { page--; render(); } });
    if (next) {
        next.addEventListener("click", function () {
            var totalPages = Math.max(1, Math.ceil(rows.filter(matches).length / pageSize));
            if (page < totalPages) { page++; render(); }
        });
    }

    render();
})();

// Names the portfolio account this browser is logged in to on the portfolio login links, when the portfolio says who that is.
(() => {
    const links = document.querySelectorAll(".portfolio-button");
    if (!links.length) return;
    fetch(`${links[0].dataset.portfolioUrl}/sso/me`, { credentials: "include", signal: AbortSignal.timeout(5000) })
        .then((response) => (response.ok ? response.json() : {}))
        .then((data) => {
            if (data.username) links.forEach((link) => { link.textContent = `Log in as ${data.username}`; });
        })
        .catch(() => {});
})();
