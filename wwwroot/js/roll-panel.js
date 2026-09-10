// Shared roller front end, used by Oracles/Roll and Flows/Run.
//
// This was inline in Roll.cshtml until Flows needed the same three things: post a form as JSON
// with a plain-submit fallback, render an outcome into a panel, and keep a paged table of the
// outcomes so far.
//
// Deliberately plain: no framework, no innerHTML, everything built with createElement and
// textContent, matching the rest of wwwroot/js. The element hooks are data-roll-* attributes
// looked up inside a root element rather than global ids, so a page can hold more than one
// panel or log. Views/Shared/_RollPanel.cshtml renders the markup these names expect.

window.GmRoll = (function () {
    "use strict";

    var NO_TEXT = "(no result text)";

    // The headline of an outcome: dice show the total they rolled, cards show the card.
    function headline(data) {
        return data.isDice ? String(data.value) : data.key;
    }

    // POSTs a form the way the server's IsAjaxRequest check expects. FormData carries the
    // antiforgery token along with everything else, so nothing has to be threaded in by hand.
    function postForm(form, submitter) {
        var body = new FormData(form);

        // A submit button's own name and value are part of a submission only when that button
        // is the one pressed, and FormData built from the form alone cannot know which was.
        if (submitter && submitter.name) {
            body.append(submitter.name, submitter.value);
        }

        return fetch(form.action, {
            method: "POST",
            body: body,
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
    }

    function panel(root) {
        if (!root) return null;

        function part(name) {
            return root.querySelector('[data-roll="' + name + '"]');
        }

        var resultEl = part("result");
        var labelEl = part("label");
        var valueEl = part("value");
        var captionEl = part("caption");
        var textEl = part("text");
        var singleEl = part("single");
        var listEl = part("list");
        var reshuffledEl = part("reshuffled");
        var errorEl = part("error");

        function fillItem(item) {
            valueEl.textContent = headline(item);
            captionEl.textContent = item.caption || "";
            textEl.textContent = item.result || NO_TEXT;
            textEl.classList.toggle("text-muted", !item.result);
        }

        function fillList(items) {
            listEl.textContent = "";
            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                var li = document.createElement("li");
                var strong = document.createElement("strong");
                strong.textContent = headline(item);
                li.appendChild(strong);
                if (item.caption) {
                    var cap = document.createElement("span");
                    cap.className = "small text-muted ms-1";
                    cap.textContent = item.caption;
                    li.appendChild(cap);
                }
                var text = document.createElement("div");
                text.textContent = item.result || NO_TEXT;
                if (!item.result) text.className = "text-muted";
                li.appendChild(text);
                listEl.appendChild(li);
            }
        }

        return {
            showOutcome: function (data, label, outcomes) {
                errorEl.hidden = true;

                if (labelEl) {
                    labelEl.textContent = label || "";
                    labelEl.hidden = !label;
                }

                var items = outcomes && outcomes.length
                    ? outcomes
                    : (data && data.outcomes && data.outcomes.length ? data.outcomes : [data]);
                var many = items.length > 1;

                if (singleEl) singleEl.hidden = many;
                if (listEl) listEl.hidden = !many;

                if (many) {
                    fillList(items);
                } else {
                    fillItem(items[0] || data);
                }

                var reshuffled = false;
                for (var r = 0; r < items.length; r++) {
                    if (items[r] && items[r].reshuffled) { reshuffled = true; break; }
                }
                reshuffledEl.hidden = !reshuffled;
                resultEl.hidden = false;
            },

            showError: function (message) {
                resultEl.hidden = true;
                errorEl.textContent = message;
                errorEl.hidden = false;
            },

            clear: function () {
                resultEl.hidden = true;
                errorEl.hidden = true;
            }
        };
    }

    // A paged table of outcomes.
    //
    // options:
    //   heading      base text for the heading, "(Page n)" is appended when it pages
    //   pageSize     rows per page
    //   newestFirst  true for a roll log, false for a flow transcript that reads downwards
    //   showLabel    render a leading column naming which step produced each row
    //   limit        how many rows to keep at all
    function log(wrap, options) {
        if (!wrap) return null;

        var opts = options || {};
        var heading = opts.heading || "Log";
        var pageSize = opts.pageSize || 5;
        var newestFirst = opts.newestFirst !== false;
        var showLabel = opts.showLabel === true;
        var limit = opts.limit || 50;

        function part(name) {
            return wrap.querySelector('[data-log="' + name + '"]');
        }

        var headingEl = part("heading");
        var bodyEl = part("body");
        var pager = part("pager");
        var prev = part("prev");
        var next = part("next");
        var info = part("info");

        var rows = [];
        var page = 1;

        function cell(text, muted) {
            var td = document.createElement("td");
            td.textContent = text;
            if (muted) td.className = "text-muted";
            return td;
        }

        function makeRow(data) {
            var tr = document.createElement("tr");
            var items = data.outcomes && data.outcomes.length > 1 ? data.outcomes : null;

            if (showLabel) {
                tr.appendChild(cell(data.label || ""));
            }

            if (items) {
                var listCell = document.createElement("td");
                listCell.colSpan = 2;
                var ol = document.createElement("ol");
                ol.className = "mb-0 ps-3";
                for (var i = 0; i < items.length; i++) {
                    var item = items[i];
                    var li = document.createElement("li");
                    var strong = document.createElement("strong");
                    strong.textContent = headline(item);
                    li.appendChild(strong);
                    if (item.caption) {
                        var cap = document.createElement("span");
                        cap.className = "small text-muted ms-1";
                        cap.textContent = item.caption;
                        li.appendChild(cap);
                    }
                    var text = document.createElement("span");
                    text.className = item.result ? "ms-1" : "ms-1 text-muted";
                    text.textContent = item.result || NO_TEXT;
                    li.appendChild(text);
                    ol.appendChild(li);
                }
                listCell.appendChild(ol);
                tr.appendChild(listCell);
                return tr;
            }

            var keyCell = document.createElement("td");
            var strong = document.createElement("strong");
            strong.textContent = headline(data);
            keyCell.appendChild(strong);

            if (data.caption) {
                var caption = document.createElement("div");
                caption.className = "small text-muted";
                caption.textContent = data.caption;
                keyCell.appendChild(caption);
            }

            tr.appendChild(keyCell);
            tr.appendChild(cell(data.result || NO_TEXT, !data.result));
            return tr;
        }

        function render() {
            var totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
            if (page > totalPages) page = totalPages;
            if (page < 1) page = 1;

            var start = (page - 1) * pageSize;
            var slice = rows.slice(start, start + pageSize);

            bodyEl.textContent = "";
            for (var i = 0; i < slice.length; i++) {
                bodyEl.appendChild(makeRow(slice[i]));
            }

            wrap.hidden = rows.length === 0;

            var paged = rows.length > pageSize;
            if (headingEl) {
                headingEl.textContent = paged ? heading + " (Page " + page + ")" : heading;
            }

            pager.hidden = !paged;
            if (paged) {
                info.textContent = "Page " + page + " of " + totalPages;
                prev.disabled = page === 1;
                next.disabled = page === totalPages;
            }
        }

        prev.addEventListener("click", function () {
            if (page > 1) { page--; render(); }
        });
        next.addEventListener("click", function () {
            page++;
            render();
        });

        return {
            add: function (data) {
                if (newestFirst) {
                    rows.unshift(data);
                    if (rows.length > limit) rows.length = limit;
                    page = 1;
                } else {
                    rows.push(data);
                    if (rows.length > limit) rows.shift();
                    // Follow the run to whichever page the newest row landed on.
                    page = Math.ceil(rows.length / pageSize);
                }
                render();
            },

            reset: function () {
                rows = [];
                page = 1;
                render();
            },

            render: render
        };
    }

    // Intercepts a form submit, posts it, and hands the parsed JSON to onData.
    //
    // Any failure at all — a non-OK response, a network error, unparseable JSON — falls back to
    // submitting the form normally, which the server answers with a redirect. That is the whole
    // reason both response shapes exist.
    function bind(form, handlers) {
        if (!form) return;

        var onData = handlers.onData || function () { };
        var onError = handlers.onError || function () { };

        form.addEventListener("submit", function (event) {
            event.preventDefault();

            // The button actually pressed, so the right one goes disabled while the request
            // is out and its value reaches the server. A form with two submit buttons would
            // otherwise always report the first.
            var button = event.submitter || form.querySelector("button[type=submit]");
            if (button && button.disabled) return;
            if (button) button.disabled = true;

            postForm(form, button)
                .then(function (response) {
                    if (!response.ok) { form.submit(); return null; }
                    return response.json();
                })
                .then(function (data) {
                    if (!data) return;
                    if (data.error) { onError(data.error); return; }
                    onData(data);
                })
                .catch(function () { form.submit(); })
                .finally(function () { if (button) button.disabled = false; });
        });
    }

    return {
        postForm: postForm,
        panel: panel,
        log: log,
        bind: bind,
        headline: headline
    };
})();
