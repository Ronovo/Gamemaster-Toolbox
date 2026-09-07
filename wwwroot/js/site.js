// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

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
