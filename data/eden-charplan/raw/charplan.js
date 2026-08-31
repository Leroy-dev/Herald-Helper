/*-----------------------------
 * Eden DAoC Charplan @ Adam
 * Created: 2022-07-01
 * Updated: 2023-07-13
 ------------------------------*/

var v = "1.0.62";//version
var debug = true;
var c = getparam("c") || "Animist";//class
var showids = false;

//retrieve from cookie
if (!getparam("l")) getcookie();

var l = Math.max(1, Math.min(50, (getparam("l") || 50) * 1));//level
var r = getparam("r") || "Celt";//race
var rr = getparam("rr") || "5L0";//realmrank
var p = getparam("p") || "s";//page([s]kill/[r]ealmabilities)


var daoc = {};
var spells = [];
var styles = [];
var icons = [];
var json = null;
var jsoncl = null;
var specs = {};
var skills = {};
var ras = {};
var specs_level = {};
var ras_level = {};
var cls = {};
var cls_trained = {};
var ready = false;
var points_multiplier = 1.0;
var points_total = 0;
var points_remain = 0;
var points_used = 0;
var last_apply = 0;
var pinned_el = null;

function pin_hint_html() {
    return "<div class=\"pin-hint\">Right-click to " + (pinned_el ? "unpin" : "pin") + "</div>";
}

function toggle_pin(e) {
    e.preventDefault();
    let $el = $(this);
    let was_this = pinned_el && pinned_el.is($el);
    if (pinned_el) pinned_el.removeClass("pinned");
    pinned_el = was_this ? null : $el;
    if (pinned_el) pinned_el.addClass("pinned");
    $el.trigger("mouseover");
    return false;
}


$(document).ready(function () {

    $(document).keypress(function (e) {
        if (e.key === "d") {
            showids = !showids;
            messagebox_show("Debug mode " + (showids ? "activated" : "desactivated"));
        }
    });

    $("meta[name='viewport']").attr("content", "width=device-width, initial-scale=1");

    $("#directurl").click(function () {
        $("#directurl").select();
    });
    // The converted tab/Share links keep button-style keyboard activation:
    // Space always (anchors ignore it natively), Enter only before the first
    // update_url() gives them an href (native Enter handling takes over then)
    $(document).on("keydown", "a.global-btn", function (e) {
        if (e.which === 32 || (e.which === 13 && !this.getAttribute("href"))) {
            e.preventDefault();
            this.click();
        }
    });

    $("#directurlcc").click(function (e) {
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || e.which !== 1) return;
        e.preventDefault();
        $("#directurl").prop("type", "text");
        $("#directurl").select();
        document.execCommand("copy");
        $("#directurl").prop("type", "hidden");
        messagebox_show("The direct link is copied to your clipboard, you can now paste the link anywhere (Ctrl + V).");
    });

    $("#apply").click(function () {
        if (last_apply > 0 && last_apply + 5000 > new Date().getTime()) {
            messagebox_show("Slow down!");
            return;
        }
        last_apply = new Date().getTime();

        if (discordid === null) {
            messagebox_show("You need to login your " + c + " level " + l + " character to apply this build.");
            return;
        } else {
            $("#apply").css("visibility", "hidden");
            $.get("chrplan/apply.php" + location.search, function (result) {
                var msg;
                switch (result) {
                    case "success":
                        msg = "Your request has been successfully sent to the Game server. Check ingame!";
                        break;
                    case "error:":
                        msg = "Connect your <strong>" + c + "</strong> ingame to apply this build.<br/><br/>Make sure you are logged in with the right character (class and name must match).";
                        break;
                    case "error:area":
                        msg = "You need to be in a valid area to apply a build:<br/><br/>Capital City, Housing, Bind Stone, or near any Trainer.";
                        break;
                    case "error:notregistred":
                        msg = "You need to be logged in on the website to apply a build.";
                        break;
                    case "error:discordid":
                    case "error:ip":
                        msg = "Your account is not properly linked. Make sure your Discord account is connected to your website profile.";
                        break;
                    default:
                        msg = "An error occurred while processing your request.<br/><br/>[" + result + "]";
                }
                messagebox_show(msg);
                $("#apply").css("visibility", "visible");
            });
        }
    });

    $("#reset").click(function () {
        $("#levels").val("50");
        l = 50;
        update_totalpoints();
        if (p === "s") {
            $.each(specs_level, function (spec_id, level) {
                update_speclevel(spec_id, 1);
            });
        } else if (p === "r") {
            $.each(ras_level, function (ra_id, level) {
                update_ralevel(ra_id, 0);
            });
        } else if (p === "m") {
            update_clslevel();
        }
    });

    $("#hidder").click(function () {
        if (messagebox_timer !== null)
            clearTimeout(messagebox_timer);
        $("#hidder").fadeOut();
    });

    if (p === "r")
        $("#ras").addClass("active");
    else if (p === "s")
        $("#skills").addClass("active");
    else
        $("#misc").addClass("active");

    $("#rr").val(rr);

    $("#skills").click(function (e) {
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || e.which !== 1) return;
        e.preventDefault();
        if (p === "s")
            return;
        $("#skills").addClass("active");
        $("#ras").removeClass("active");
        $("#misc").removeClass("active");
        $("#abilities, #specs, #details").empty();
        p = "s";
        build_specs();
        update_url();
    });

    $("#ras").click(function (e) {
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || e.which !== 1) return;
        e.preventDefault();
        if (p === "r")
            return;
        $("#skills").removeClass("active");
        $("#ras").addClass("active");
        $("#misc").removeClass("active");
        $("#abilities, #specs, #details").empty();
        p = "r";
        build_ras();
        update_url();
    });

    $("#misc").click(function (e) {
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey || e.which !== 1) return;
        e.preventDefault();
        if (p === "m")
            return;
        $("#skills").removeClass("active");
        $("#ras").removeClass("active");
        $("#misc").addClass("active");
        $("#abilities, #specs, #details").empty();
        p = "m";
        build_misc();
    });

    //select levels:
    for (let i = 50; i > 0; i--) {
        if (i >= 40 && i < 50) {
            $("#levels").append($("<option>", {
                value: i + 0.5,
                text: i + 0.5,
                class: "neutral-list"
            }));
        }
        $("#levels").append($("<option>", {
            value: i,
            text: i,
            class: "neutral-list"
        }));
    }
    $("#levels option[value='" + l + "']").prop("selected", true);
    $("#levels").change(function () {
        l = $("#levels").val() * 1;
        update_totalpoints();

        $(".spec_skill[data-base='1']").filter(function () {
            return $(this).data("level") * 1 <= l;
        }).addClass("selected");
        $(".spec_skill[data-base='1']").filter(function () {
            return $(this).data("level") * 1 > l;
        }).removeClass("selected");

        update_url();
    });
    $("#levels").change();

    $("#rr_plus").click(function () {
        update_rr(+1);
        update_totalpoints();
    });
    $("#rr_minus").click(function () {
        update_rr(-1);
        update_totalpoints();
    });
    $("#rr").change(function () {
        rr = $(this).val();
        update_rr(0);
    });

    window.onscroll = function () {
        let diff = window.pageYOffset - $("#details").offset().top;
        let specs_height = $("#specs").outerHeight(true);
        //if (debug) console.log("specs_height", specs_height);
        let details_height = $("#details table").outerHeight(true);
        let diff2 = $(window).height() - details_height;
        diff2 -= 40;
        if (diff2 > 0) diff2 = 0;
        diff += diff2;
        diff += 20;//add some margin top
        if (diff < 0) diff = 0;

        if (details_height + diff > specs_height) {
            diff = specs_height - details_height;
        }

        $("#details").css("paddingTop", diff);
    }

    if (window.history && window.history.pushState) {
        $(window).on("popstate", function (e) {
            if (!getparam("l")) getcookie();

            l = Math.max(1, Math.min(50, (getparam("l") || 50) * 1));//level
            r = getparam("r") || "Celt";//race
            rr = getparam("rr") || "5L0";//realmrank
            p = getparam("p") || "s";//page([s]kill/[r]ealmabilities)

            $("#ras").removeClass("active");
            $("#skills").removeClass("active");
            $("#misc").removeClass("active");

            if (p === "r")
                $("#ras").addClass("active");
            else if (p === "s")
                $("#skills").addClass("active");
            else
                $("#misc").addClass("active");

            if (p === "s")
                build_specs();
            else if (p == "r")
                build_ras();
            else
                build_misc();
        });
    }

    load_data();
});

function build_specs() {
    if (debug) console.log("build_specs");
    ready = false;
    pinned_el = null;
    $("#details").empty();
    let details = "<table cellpadding=\"0\" cellspacing=\"0\">"
        + "<tr><td class=\"title_totalpoints\">Total skill points</td><td class=\"title_multiplier\">Multiplier</td></tr>"
        + "<tr><td id=\"points_total\"></td><td id=\"points_mult\"></td></tr>"
        + "<tr><td class=\"title_remaining\">Remaining</td><td class=\"title_used\">Used</td></tr>"
        + "<tr><td id=\"points_remain\"></td><td id=\"points_used\"></td></tr>"
        + "<tr><td id=\"info\" colspan=\"2\"></td></tr>"
        + "</table>";
    $("#details").html(details);

    $("#specs").empty();
    let html = "<table cellpadding=\"0\" cellspacing=\"0\">";
    $.each(json, function (index, val) {
        if (val.name === c) {
            points_multiplier = val.specPointMultiplier * 1;
            $("#points_mult").html(points_multiplier.toFixed(1));
            $("#levels").change();

            let len = val.specs.length;
            for (let i = 0; i < len; i++) {
                let spec = val.specs[i];
                specs[spec.id] = spec;
                specs_level[spec.id] = (getparam("s" + spec.id) * 1) || 1;

                if (spec.base === true)
                    html += "<tr class=\"spec_title\"><td class=\"spec_title\" colspan=\"3\">" + spec.name + "</td></tr>";
                else
                    html += "<tr class=\"spec_title\"><td class=\"spec_title\">" + spec.name + "</td><td class=\"spec_points\" id=\"spec_points_" + spec.id + "\">0" + (spec.autotrain === true ? "&nbsp;&nbsp;&nbsp;(Autotrain)" : "") + "</td><td class=\"spec_buttons\">"
                        + "<button class=\"spec_minus\" title=\"Decrease\" data-id=\"" + spec.id + "\">-</button><span class=\"spec_level\" id=\"spec_level_" + spec.id + "\">" + specs_level[spec.id] + "</span><button class=\"spec_plus\" title=\"Increase\" data-id=\"" + spec.id + "\">+</button></td></tr>";

                let len3 = spec.skillGroups.length;
                for (let i3 = 0; i3 < len3; i3++) {
                    let skillGroup = spec.skillGroups[i3];
                    html += "<tr class=\"spec_skillgroup" + (i3 + 1 == len3 ? " last" : "") + "\"><td class=\"spec_skillgroup\">" + skillGroup.name + "</td><td colspan=\"2\">";

                    let len4 = skillGroup.skills.length;
                    for (let i4 = 0; i4 < len4; i4++) {
                        let skill = skillGroup.skills[i4];
                        let name = skill.level;
                        //special naming cases:
                        if (spec.base === true) {
                            switch (skillGroup.name.toLowerCase()) {
                                case "armor":
                                case "weapons":
                                    if (skill.level === 1)
                                        name = skill.name;
                                    break;
                                case "quickcast":
                                    if (skill.level === 1)
                                        name = "30 sec";
                                    break;
                                case "parry":
                                case "tireless":
                                    if (skill.level === 1)
                                        name = "Yes";
                                    break;
                                default:
                                    name = skill.level;
                                    break;
                            }
                        }
                        html += "<span class=\"spec_skill" + (spec.base === true ? "_base" : "") + "\" data-id=\"" + skill.id + "\" data-base=\"0\" data-spec=\"" + spec.id + "\" data-level=\"" + skill.level + "\">" + name + "</span>";
                        skills[spec.id + "_" + skill.id] = skill;
                    }
                    html += "</td></tr>";
                }

                //baseline
                $.each(spec.spellLines, function (i3, spellLine) {
                    if (spellLine.base !== true)
                        return;

                    html += "<tr class=\"spec_spellline\"><td class=\"spec_spellline\" colspan=\"3\">" + spellLine.name + "</td></tr>";

                    let len3 = spellLine.spellGroups.length;
                    for (let i3 = 0; i3 < len3; i3++) {
                        let spellGroup = spellLine.spellGroups[i3];
                        html += "<tr class=\"spec_spellgroup" + (i3 + 1 == len3 ? " last" : "") + "\"><td class=\"spec_spellgroup\">" + spellGroup.name + "</td><td colspan=\"2\">";

                        let len = spellGroup.skills.length;
                        for (let i4 = 0; i4 < len; i4++) {
                            let skill = spellGroup.skills[i4];
                            html += "<span class=\"spec_skill" + (skill.level <= l ? " selected" : "") + "\" data-id=\"" + skill.id + "\" data-base=\"1\" data-spec=\"" + spec.id + "\" data-level=\"" + skill.level + "\">" + skill.level + "</span>";
                            skills[spec.id + "_" + skill.id] = skill;
                        }
                        html += "</td></tr>";
                    }
                });
                //specline
                $.each(spec.spellLines, function (i3, spellLine) {
                    if (spellLine.base === true)
                        return;

                    html += "<tr class=\"spec_spellline\"><td class=\"spec_spellline\" colspan=\"3\">" + spellLine.name + "</td></tr>";

                    let len3 = spellLine.spellGroups.length;
                    for (let i3 = 0; i3 < len3; i3++) {
                        let spellGroup = spellLine.spellGroups[i3];
                        html += "<tr class=\"spec_spellgroup" + (i3 + 1 == len3 ? " last" : "") + "\"><td class=\"spec_spellgroup\">" + spellGroup.name + "</td><td colspan=\"2\">";

                        let len4 = spellGroup.skills.length;
                        for (let i4 = 0; i4 < len4; i4++) {
                            let skill = spellGroup.skills[i4];
                            html += "<span class=\"spec_skill\" data-id=\"" + skill.id + "\" data-base=\"0\" data-spec=\"" + spec.id + "\" data-level=\"" + skill.level + "\">" + skill.level + "</span>";
                            skills[spec.id + "_" + skill.id] = skill;
                        }
                        html += "</td></tr>";
                    }
                });
            }

            len = val.realmAbilities.length;
            for (let i = 0; i < len; i++) {
                let ra = val.realmAbilities[i];
                ras_level[ra.id] = (getparam("a" + ra.id) * 1) || 0;
            }
        }
    });
    html += "</table>";
    $("#specs").html(html);

    $.each(specs_level, function (spec_id, level) {
        if (level <= 1)
            return;
        $(".spec_skill[data-base='0'][data-spec='" + spec_id + "']").filter(function () {
            return $(this).data("level") * 1 <= level;
        }).addClass("selected");

        update_speclevel(spec_id, specs_level[spec_id]);
    });

    $(".spec_minus").click(function () {
        let id = $(this).data("id") * 1;
        update_speclevel(id, specs_level[id] - 1);
    });
    $(".spec_plus").click(function () {
        let id = $(this).data("id") * 1;
        update_speclevel(id, specs_level[id] + 1);
    });

    $(".spec_skill").on("contextmenu", toggle_pin);
    $(".spec_skill").mouseover(function () {
        if (pinned_el && !pinned_el.is(this)) return;
        let skill = skills[$(this).data("spec") + "_" + $(this).data("id")];

        //hardcode Guard
        let guard = false;
        if ($(this).data("spec") * 1 === 73 && $(this).data("id") * 1 === 8 && ($(this).data("level") * 1 === 5 || $(this).data("level") * 1 === 10 || $(this).data("level") * 1 === 15)) {
            guard = true;
            skill = {
                "attributes": [
                    [
                        "Assigning oneself to protect another player and block incoming enemy blows on the protected player. This is only available by specializing in the Shield specialization line."
                    ]
                ],
                "id": 8,
                "level": $(this).data("level") * 1,
                "name": "Guard " + ($(this).data("level") * 1 == 5 ? "I" : ($(this).data("level") * 1 == 10 ? "II" : "III")),
                "shortInfo": null,
                "skillType": 0,
                "subSkills": []
            };
        }
        //hardcode Engage
        let engage = false;
        if ($(this).data("spec") * 1 === 73 && $(this).data("id") * 1 === 38 && $(this).data("level") * 1 === 7) {
            engage = true;
            skill = {
                "attributes": [
                    [
                        "Endurance is drained in return for a significantly increased blocking rate."
                    ]
                ],
                "id": 38,
                "level": $(this).data("level") * 1,
                "name": "Engage",
                "shortInfo": null,
                "skillType": 0,
                "subSkills": []
            };
        }

        if (debug) console.log("spec_skill.mouseover", skill);
        $("#info").empty();
        let html = "";

        let build_info = function (sk, is_subskill) {
            let valid = true;
            let percent_type = false;
            let type = "";
            let len2 = sk.attributes.length;

            for (let i2 = 0; i2 < len2; i2++) {
                let attr = sk.attributes[i2];

                if (attr[0] === "Type" && attr[1] === "Play Cast Animation") {
                    valid = false;
                    break;
                }

                if (attr[0] === "Type") type = attr[1];

                //for these spelltypes, only value is percent
                if (attr[0] === "Type" && (attr[1].indexOf("Resist Buff") >= 0
                    || attr[1].indexOf("Celerity Buff") >= 0
                    || attr[1].indexOf("Combat Speed") >= 0
                    || attr[1].indexOf("Cast Speed") >= 0
                    || attr[1].indexOf("Waterbreathing") >= 0
                    || attr[1].indexOf("Root") >= 0
                    || attr[1].indexOf("Snare") >= 0
                    || attr[1].indexOf("Fatigue Consumption Buff") >= 0
                    || attr[1].indexOf("Resurrect") >= 0
                    || attr[1].indexOf("Direct Damage With Debuff") >= 0
                    || attr[1].indexOf("Damage Speed Decrease") >= 0
                    || attr[1].indexOf("Mesmerize Duration") >= 0
                    || attr[1].indexOf("Lifedrain") >= 0
                    || attr[1].indexOf("Amnesia") >= 0
                    || attr[1].indexOf("Armor Absorption") >= 0
                    || attr[1].indexOf("Nearsight") >= 0
                    || attr[1].indexOf("Piercing Magic") >= 0
                    || attr[1].indexOf("Confusion") >= 0
                    || attr[1].indexOf("Archery") >= 0
                    || attr[1].indexOf("Savage ") >= 0)) {
                    percent_type = true;
                }
            }

            if (valid) {
                html += "<table cellpadding=\"0\" cellspacing=\"0\">";
                html += "<tr><td class=\"spec_skill_name\" colspan=\"2\"><table style=\"width: 100%; height: 100%;\" cellpadding=\"0\" cellspacing=\"0\"><tr><td style=\"width: 32px; height: 32px;\">" + build_icon(sk) + "</td><td style=\"text-align: center;\">" + sk.name + "</td><td style=\"width: 32px;\"></td></tr></table></td></tr>";
                if (!is_subskill) {
                    html += "<tr><td class=\"spec_skill_level\" style=\"width: 50%;\">Level</td><td>" + sk.level + "</td></tr>";
                }
                $.each(sk.attributes, function (i, attr) {
                    if (attr[0] == "Growth Rate")
                        attr[1] = (attr[1].replace(",", ".") * 1).toFixed(2);

                    //these are always percent
                    let percent_attr = (attr[0] === "Resurrect Health"
                        || attr[0] === "Resurrect Mana"
                        || attr[0] === "Resurrect Endurance"
                        || attr[0] === "Life Drain Return"
                        || attr[0] === "Amnesia Chance"
                        || attr[0] === "Confuse Ally Chance"
                        || attr[0] === "Proc Chance");

                    let key = attr[0];
                    let val = attr[1];
                    if (type === "Archery" && attr[0] === "Power") {
                        key = "Endurance";
                        val = (0 - (val * 1)) + "%";
                    }

                    if (type == "Disease" && attr[0] == "Damage") {
                        key = "Snare";
                        val = (/*0 -*/ (val * 1)) + "%";
                    }
                    else if (type == "Disease" && attr[0] == "Value") {
                        key = "Strength";
                        val = (0 - (val * 1)) /*+ "%"*/;
                    }

                    if (guard || engage)
                        html += "<tr><td class=\"spec_skill_attr\" colspan=\"2\">" + key + "</td></td></tr>";
                    else
                        html += "<tr><td class=\"spec_skill_attr\">" + key + "</td><td>" + val + ((((percent_type && key === "Value") || percent_attr) && val.indexOf("%") < 0) ? "%" : "") + "</td></tr>";
                });

                if ((showids || sk.skillType === 2) && sk.id) {
                    html += "<tr><td class=\"spec_skill_attr\">(Internal ID)</td><td>" + sk.id + "</td></tr>";
                    if (sk.icon != 0)
                        html += "<tr><td class=\"spec_skill_attr\">(Icon ID)</td><td>" + sk.icon + "</td></tr>";
                    //html += "<tr><td class=\"spec_skill_attr\">(IconIB)</td><td>" + (sk.skillType === 2 && sk.icon ? spells[sk.icon] : "") + "</td></tr>";
                }

                html += "</table>";
            }

            if (sk.subSkills) {
                let len = sk.subSkills.length;
                for (let i = 0; i < len; i++) {
                    let subskill = sk.subSkills[i];
                    build_info(subskill, true);
                }
            }
        }

        build_info(skill, false);

        $("#info").html(html + pin_hint_html());
    });

    $(".spec_skill_base").on("contextmenu", toggle_pin);
    $(".spec_skill_base").mouseover(function () {
        if (pinned_el && !pinned_el.is(this)) return;
        let skill = skills[$(this).data("spec") + "_" + $(this).data("id")];
        let level = $(this).data("level");
        if (debug) console.log("spec_skill_base.mouseover", skill);
        $("#info").empty();
        let html = "";

        let build_info_base = function (sk) {
            let len2 = sk.attributes.length;

            for (let i2 = 0; i2 < len2; i2++) {
                let attr = sk.attributes[i2];
                html += "<table cellpadding=\"0\" cellspacing=\"0\">";
                html += "<tr><td class=\"spec_skill_name\" colspan=\"2\">" + sk.name + "</td></tr>";
                html += "<tr><td class=\"spec_skill_level\" style=\"width: 50%;\">Level</td><td>" + level + "</td></tr>";
                $.each(sk.attributes, function (i, attr) {
                    html += "<tr><td class=\"spec_skill_attr\" colspan=\"2\">" + attr[0] + "</td></tr>";
                });
                html += "</table>";
            }
        }

        build_info_base(skill);

        $("#info").html(html + pin_hint_html());
    });

    $(".spec_skill").click(function () {
        let $cell = $(this);
        let spec_id = $cell.data("spec");
        let level = $cell.data("level") * 1;
        let is_mastery = ($cell.data("base") * 1) === 0;
        let cur = specs_level[spec_id] * 1;
        if (debug) console.log("spec_skill.click", "spec:", spec_id, "level:", level, "cur:", cur, "mastery:", is_mastery);
        if (is_mastery && cur === level) {
            let $row = $cell.closest("tr");
            let min_level = Infinity;
            $row.find(".spec_skill[data-base='0']").each(function () {
                let lv = $(this).data("level") * 1;
                if (lv < min_level) min_level = lv;
            });
            if (level === min_level) {
                update_speclevel(spec_id, 1);
                return;
            }
        }
        update_speclevel(spec_id, level);
    });
    ready = true;
    update_permalinks();
}

function build_ras() {
    if (debug) console.log("build_ras");
    ready = false;
    pinned_el = null;
    $("#details").empty();
    let details = "<table cellpadding=\"0\" cellspacing=\"0\">"
        + "<tr><td class=\"title_totalpoints\">Total skill points</td><td class=\"title_multiplier\"></td></tr>"
        + "<tr><td id=\"points_total\"></td><td id=\"points_mult\"></td></tr>"
        + "<tr><td class=\"title_remaining\">Remaining</td><td class=\"title_used\">Used</td></tr>"
        + "<tr><td id=\"points_remain\"></td><td id=\"points_used\"></td></tr>"
        + "<tr><td id=\"info\" colspan=\"2\"></td></tr>"
        + "</table>";
    $("#details").html(details);

    $("#specs").empty();
    ras = {};
    let html = "<table cellpadding=\"0\" cellspacing=\"0\">";
    $.each(json, function (i, val) {
        if (val.name === c) {
            let bycost = {};
            let len = val.realmAbilities.length;
            for (let i = 0; i < len; i++) {
                let ra = val.realmAbilities[i];
                ras[ra.id] = ra;
                ras_level[ra.id] = (getparam("a" + ra.id) * 1) || 0;
                if (ra.costScheme === 6) ra.costScheme = 5;

                if (ra.costScheme === 5 && ra.levels.length === 1) {//rr5
                    ra.costScheme = 10;
                }

                if (!(ra.costScheme in bycost)) {
                    bycost[ra.costScheme] = [];
                }
                bycost[ra.costScheme].push(ra);
            }

            $.each(bycost, function (cost, list) {
                let cat = "";
                let active = false;
                switch (cost * 1) {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        cat = "Passives";
                        break;
                    case 5:
                    case 10:
                        cat = "Actives";
                        active = true;
                        break;
                }
                html += "<tr class=\"spec_racat\"><td class=\"spec_racat\">" + cat + "</td><td><table class=\"costscheme\" cellpadding=\"0\" cellspacing=\"0\"><tr>";
                if (cost * 1 !== 10) {
                    let len = list[0].levels.length;
                    let total = 0;
                    for (let i = 0; i < len; i++) {
                        let pts = list[0].levels[i].cost;
                        total += pts;
                        html += "<td class=\"spec_rapts\" style=\"width: " + Math.floor(100 / len) + "%;\">" + pts + (i > 0 ? "&nbsp;&nbsp;(" + total + ")" : "") + "</td>";
                    }
                }
                html += "</tr></table></td></tr>";

                len = list.length;
                for (let i = 0; i < len; i++) {
                    let ra = list[i];
                    html += "<tr class=\"spec_ragroup" + (i + 1 == len ? " last" : "") + "\" data-id=\"" + ra.id + "\"><td class=\"spec_ragroup\">" + ra.name + (active ? "<span class=\"spec_raactive\">" + ra.levels[0].cooldown + "</span>" : "") + "</td><td><table class=\"costscheme\" cellpadding=\"0\" cellspacing=\"0\"><tr>";
                    let len2 = ra.levels.length;
                    for (let i2 = 0; i2 < len2; i2++) {
                        let level = ra.levels[i2];
                        html += "<td class=\"spec_rainfo\" style=\"width: " + Math.floor(100 / len2) + "%;\" data-id=\"" + ra.id + "\" data-level=\"" + (i2 + 1) + "\">" + level.shortInfo.replace(" levels", " lvl") + "</td>";
                    }
                    html += "</tr></table></td></tr>";
                }
            });

            len = val.specs.length;
            for (let i = 0; i < len; i++) {
                let spec = val.specs[i];
                specs_level[spec.id] = (getparam("s" + spec.id) * 1) || 1;
            }
        }
    });
    html += "</table>";
    $("#specs").html(html);

    $.each(ras_level, function (ra_id, level) {
        if (level <= 0)
            return;
        $(".spec_rainfo[data-id='" + ra_id + "']").filter(function () {
            return $(this).data("level") * 1 <= level;
        }).addClass("selected");

        update_ralevel(ra_id, ras_level[ra_id]);
    });

    $("tr.spec_ragroup").on("contextmenu", toggle_pin);
    $("tr.spec_ragroup").mouseover(function () {
        if (pinned_el && !pinned_el.is(this)) return;
        let id = $(this).data("id");
        let ra = ras[id];
        html = "<table cellpadding=\"0\" cellspacing=\"0\">";
        html += "<tr><td class=\"spec_skill_name\" colspan=\"2\"><table style=\"width: 100%; height: 100%;\" cellpadding=\"0\" cellspacing=\"0\"><tr><td style=\"width: 32px; height: 32px;\">" + build_icon(ra) + "</td><td style=\"text-align: center;\">" + ra.name + "</td><td style=\"width: 32px;\"></td></tr></table></td></tr>";
        let spl = ra.description.split("\r\n");

        html += "<tr><td class=\"spec_skill_desc\" colspan=\"2\">" + spl[0] + "</td></tr>";
        let len = spl.length
        if (len > 1) {
            for (let i = 1; i < len; i++) {
                if (spl[i].indexOf(":") >= 0) {
                    let spl2 = spl[i].split(":");
                    let info = spl2.splice(1, spl2.length - 1).join(":")
                        .replace(" seconds", " sec").replace(" second", " sec")
                        .replace(" minutes", " min").replace(" minute", " min")
                        .replace("movement speed", "speed").replace("range detection", "range");
                    html += "<tr class=\"spec_skill_desc_attr\"><td class=\"spec_skill_desc_attr\" style=\"width: 50%; white-space: nowrap;\">" + spl2[0] + "&nbsp;</td><td>" + info + "</td></tr>";
                } else {
                    html += "<tr class=\"spec_skill_desc_attr\"><td class=\"spec_skill_desc_attr\" colspan=\"2\">" + spl[i] + "</td></tr>";
                }
            }
        }

        if (showids && ra.id) {
            html += "<tr><td class=\"spec_skill_attr\">(Internal ID)</td><td>" + ra.id + "</td></tr>";
            html += "<tr><td class=\"spec_skill_attr\">(IconID)</td><td>" + ra.icon + "</td></tr>";
            //html += "<tr><td class=\"spec_skill_attr\">(IconIB)</td><td>" + (sk.skillType === 2 && sk.icon ? spells[sk.icon] : "") + "</td></tr>";
        }

        html += "</table>";
        $("#info").html(html + pin_hint_html());
    });

    $(".spec_rainfo").click(function () {
        let id = $(this).data("id");
        let level = $(this).data("level");
        let ra = ras[id];

        if (ras_level[id] === level)
            level--;

        update_ralevel(id, level);
    });

    update_rr();

    ready = true;
    update_permalinks();
}

function build_misc() {
    if (debug) console.log("build_misc");
    ready = false;
    pinned_el = null;
    points_total = 10;
    $("#details").empty();
    let details = "<table cellpadding=\"0\" cellspacing=\"0\">"
        + "<tr><td class=\"title_totalpoints\">Total skill points</td><td class=\"title_multiplier\"></td></tr>"
        + "<tr><td id=\"points_total\"></td><td id=\"points_mult\"></td></tr>"
        + "<tr><td class=\"title_remaining\">Remaining</td><td class=\"title_used\">Used</td></tr>"
        + "<tr><td id=\"points_remain\"></td><td id=\"points_used\"></td></tr>"
        + "<tr><td id=\"info\" colspan=\"2\"></td></tr>"
        + "</table>";
    $("#details").html(details);

    $.each(jsoncl, function (index, val) {
        let name = val.name.toLowerCase();
        cls[name] = {};
        $.each(val.skills, function (indexsk, sk) {
            cls[name][sk.item1 + "_" + sk.item2] = sk.item3;
            cls[name].id = val.id;
        });
    });

    $("#specs").empty();
    $("#specs").load("chrplan/cl.html"/* + (debug ? "?r=" + Math.random() : "")*/, function () {

        $.each(daoc.classes[c].cls, function (index, cl) {
            let name = cl.toLowerCase();
            if (name === "midrogue") name = "midgardrogue";//"stalker";
            else if (name === "albrogue") name = "albionrogue";//"stalker";
            //else if (name === "acolyte") name = "naturalist";
            //else if (name === "guardian") name = "fighter";
            //else if (name === "viking") name = "fighter";
            if (debug) console.log("cl:", name);
            $("#cl_" + name).show();
        });

        $(".cl_icon").on("contextmenu", toggle_pin);
        $(".cl_icon").mouseover(function () {
            if (pinned_el && !pinned_el.is(this)) return;
            let id = $(this).attr("id");
            let spl = id.split("_");
            let cat = spl[0];
            let col = spl[1].replace("col", "");
            let row = spl[2].replace("row", "");
            if (debug) console.log("cl.mouseover", "cat:", cat, "col:" + col, "row:" + row);
            let cl = cls[cat/*.replace("stalker", "midgardrogue")*/][col + "_" + row];

            $("#info").empty();
            let html = "";

            let build_info = function (sk, is_subskill) {
                let valid = true;
                let percent_type = false;
                let type = "";
                let len2 = sk.attributes.length;

                for (let i2 = 0; i2 < len2; i2++) {
                    let attr = sk.attributes[i2];

                    if (attr[0] === "Type" && attr[1] === "Play Cast Animation") {
                        valid = false;
                        break;
                    }

                    if (attr[0] === "Type") type = attr[1];

                    //for these spelltypes, only value is percent
                    if (attr[0] === "Type" && (attr[1].indexOf("Resist Buff") >= 0
                        || attr[1].indexOf("Celerity Buff") >= 0
                        || attr[1].indexOf("Combat Speed") >= 0
                        || attr[1].indexOf("Cast Speed") >= 0
                        || attr[1].indexOf("Waterbreathing") >= 0
                        || attr[1].indexOf("Root") >= 0
                        || attr[1].indexOf("Snare") >= 0
                        || attr[1].indexOf("Fatigue Consumption Buff") >= 0
                        || attr[1].indexOf("Resurrect") >= 0
                        || attr[1].indexOf("Direct Damage With Debuff") >= 0
                        || attr[1].indexOf("Damage Speed Decrease") >= 0
                        || attr[1].indexOf("Mesmerize Duration") >= 0
                        || attr[1].indexOf("Lifedrain") >= 0
                        || attr[1].indexOf("Amnesia") >= 0
                        || attr[1].indexOf("Armor Absorption") >= 0
                        || attr[1].indexOf("Nearsight") >= 0
                        || attr[1].indexOf("Piercing Magic") >= 0
                        || attr[1].indexOf("Confusion") >= 0
                        || attr[1].indexOf("Archery") >= 0
                        || attr[1].indexOf("Savage ") >= 0)) {
                        percent_type = true;
                    }
                }

                if (valid) {
                    html += "<table cellpadding=\"0\" cellspacing=\"0\">";
                    html += "<tr><td class=\"spec_skill_name\" colspan=\"2\">" + sk.name + "</td></tr>";
                    if (!is_subskill) {
                        html += "<tr><td class=\"spec_skill_level\" style=\"width: 50%;\">Level</td><td>" + sk.level + "</td></tr>";
                    }
                    $.each(sk.attributes, function (i, attr) {
                        if (attr[0] == "Growth Rate")
                            attr[1] = (attr[1].replace(",", ".") * 1).toFixed(2);

                        //these are always percent
                        let percent_attr = (attr[0] === "Resurrect Health"
                            || attr[0] === "Resurrect Mana"
                            || attr[0] === "Resurrect Endurance"
                            || attr[0] === "Life Drain Return"
                            || attr[0] === "Amnesia Chance"
                            || attr[0] === "Confuse Ally Chance"
                            || attr[0] === "Proc Chance");

                        let key = attr[0];
                        let val = attr[1];
                        if (type === "Archery" && attr[0] === "Power") {
                            key = "Endurance";
                            val = (0 - (val * 1)) + "%";
                        }

                        html += "<tr><td class=\"spec_skill_attr\">" + key + "</td><td>" + val + ((((percent_type && key === "Value") || percent_attr) && val.indexOf("%") < 0) ? "%" : "") + "</td></tr>";
                    });

                    if (showids && sk.id) {
                        html += "<tr><td class=\"spec_skill_attr\">(Internal ID)</td><td>" + sk.id + "</td></tr>";
                    }

                    html += "</table>";
                }

                if (sk.subSkills) {
                    let len = sk.subSkills.length;
                    for (let i = 0; i < len; i++) {
                        let subskill = sk.subSkills[i];
                        build_info(subskill, true);
                    }
                }
            }

            build_info(cl, false);

            $("#info").html(html + pin_hint_html());
        });

        $(".cl_icon").click(function () {
            let id = $(this).attr("id");
            let spl = id.split("_");
            let cat = spl[0];
            let col = spl[1].replace("col", "") * 1;
            let row = spl[2].replace("row", "") * 1;
            let key = col + "_" + row;
            let name = cat/*.replace("stalker", "midgardrogue")*/;
            let cl = cls[name][key];
            if (debug) console.log("cl.click", "cat:", cat, "col:" + col, "row:" + row, cl);

            if (name in cls_trained && key in cls_trained[name]) {
                let remcls = {};
                let valid = true;
                for (let r = 5; r >= row; r--) {
                    let c = col;
                    let k = c + "_" + r;
                    if (k in cls[name]) {
                        let remcl = cls[name][k];
                        remcls[c + "_" + r] = remcl;
                    } else {
                        if (col == 3) {
                            c = 4;
                            k = c + "_" + r;
                            if (k in cls_trained[name]) {

                                if (!("5_2" in cls_trained[name])) {
                                    let remcl = cls[name][k];
                                    remcls[k] = remcl;
                                }
                            };

                            /*c = 4;
                            k = c + "_" + r
                            if (k in cls_trained[name]) {
                                valid = false;
                                break;
                            };*/
                        } else if (col == 5) {
                            c = 4;
                            k = c + "_" + r;
                            if (k in cls_trained[name]) {

                                if (!("3_2" in cls_trained[name])) {
                                    let remcl = cls[name][k];
                                    remcls[k] = remcl;
                                }
                            };
                        }

                        /*c = col - 1;
                        k = c + "_" + r;
                        if (k in cls_trained[name]) {
                            delete cls_trained[name][k];
                            $("#" + cat + "_col" + c + "_row" + r).removeClass("active");
                        }
                        c = col + 1;
                        k = c + "_" + r;
                        if (k in cls_trained[name]) {
                            delete cls_trained[name][k];
                            $("#" + cat + "_col" + c + "_row" + r).removeClass("active");
                        }*/
                    }
                }
                if (valid) {
                    $.each(remcls, function (index, remcl) {
                        delete cls_trained[name][index];
                        let spl = index.split("_");
                        let c = spl[0];
                        let r = spl[1];
                        $("#" + cat + "_col" + c + "_row" + r).removeClass("active");
                    });
                    update_remainingpoints();
                    update_url();
                }
            } else {

                let needed = 0;
                let newcls = {};
                let left = true;
                let right = true;
                for (let r = row; r >= 1; r--) {
                    let c = col;
                    let k = c + "_" + r;
                    if (!(name in cls_trained) || !(k in cls_trained[name])) {
                        if (k in cls[name]) {
                            needed++;
                            let newcl = cls[name][k];
                            newcls[c + "_" + r] = newcl;
                        } else {
                            c = col - 1;
                            k = c + "_" + r;
                            if (!(name in cls_trained) || !(k in cls_trained[name])) {
                                left = false;
                                //needed++;
                                //let newcl = cls[name][k];
                                //newcls[c + "_" + r] = newcl;
                            }
                            c = col + 1;
                            k = c + "_" + r;
                            if (!(name in cls_trained) || !(k in cls_trained[name])) {
                                right = false;
                                //needed++;
                                //let newcl = cls[name][k];
                                //newcls[c + "_" + r] = newcl;
                            }
                        }
                    }
                }
                let valid = left || right;
                if (debug) console.log("cl.needed:", needed);
                if (valid && needed <= points_remain) {
                    $.each(newcls, function (index, newcl) {
                        if (!(name in cls_trained))
                            cls_trained[name] = {};
                        cls_trained[name][index] = newcl;
                        let spl = index.split("_");
                        let c = spl[0];
                        let r = spl[1];
                        $("#" + cat + "_col" + c + "_row" + r).addClass("active");
                    });
                    update_remainingpoints();
                    update_url();
                }
            }
        });

        for (let i = 1; i <= 12; i++) {
            let value = getparam("cl" + i);
            if (value !== null) {
                $.each(cls, function (name, cl) {
                    //console.log(name, cl);
                    if (cl.id === i) {
                        let val = parseInt(value, 36);
                        //console.log(val.toString(2));
                        for (let b = 0; b < 30; b++) {
                            let t = 1 << b;
                            if ((val & t) != 0) {
                                let col = (b % 6) + 1;
                                let row = Math.floor(b / 6) + 1;
                                let key = col + "_" + row;
                                //console.log("name:", name, "b:", b, "t:", t, "col:", col, "row:", row);
                                if (!(name in cls_trained))
                                    cls_trained[name] = {};
                                cls_trained[name][key] = cls[name][key];
                                let cat = name/*.replace("midgardrogue", "stalker")*/;
                                $("#" + cat + "_col" + col + "_row" + row).addClass("active");
                            }
                        }
                        return;
                    }
                });
            }
        }

        ready = true;
        update_totalpoints();
        update_url();
    });
}

function build_icon(sk) {
    let html = "";
    let icon = null;
    switch (sk.skillType * 1) {
        case 2:
            if (sk.icon && spells[sk.icon] && icons[spells[sk.icon]]) {
                icon = icons[spells[sk.icon]];
            }
            break;
        case 3:
            if (sk.icon && styles[sk.icon] && icons[styles[sk.icon]]) {
                icon = icons[styles[sk.icon]];
            }
            break;
        default:
            if (sk.icon && icons[sk.icon] && "costScheme" in sk) {
                icon = icons[sk.icon];
            }
            break;
    }
    if (icon !== null) {
        let ic = icon[0] % 100;
        let x = (ic % 10);
        let y = (ic - x) / 10;
        let cls = Math.floor(icon[0] / 100) * 100;
        html = "<div class=\"icon_skill icon" + cls + "\" style=\"background-position: -" + (x * 32) + "px -" + (y * 32) + "px;\">";
        if (icon[1] > 0) {
            x = (icon[1] - 1);
            y = 0;
            html += "<div class=\"icon_border\" style=\"background-position: -" + (x * 32) + "px -" + (y * 32) + "px;\"></div>";
        }
        if (icon[2] > 0) {
            x = (icon[2] - 1);
            y = 0;
            html += "<div class=\"icon_corner icon_upleft\" style=\"background-position: -" + (x * 10) + "px -" + (y * 10) + "px;\"></div>";
        }
        if (icon[3] > 0) {
            x = (icon[3] - 1);
            y = 0;
            html += "<div class=\"icon_corner icon_up icon_up\" style=\"background-position: -" + (x * 10) + "px -" + (y * 10) + "px;\"></div>";
        }
        if (icon[4] > 0) {
            x = (icon[4] - 1);
            y = 0;
            html += "<div class=\"icon_corner icon_upright\" style=\"background-position: -" + (x * 10) + "px -" + (y * 10) + "px;\"></div>";
        }
        if (icon[5] > 0) {
            x = (icon[5] - 1);
            y = 0;
            html += "<div class=\"icon_corner icon_right\" style=\"background-position: -" + (x * 10) + "px -" + (y * 10) + "px;\"></div>";
        }
        if (icon[6] > 0) {
            x = (icon[6] - 1);
            y = 0;
            html += "<div class=\"icon_corner icon_downright\" style=\"background-position: -" + (x * 10) + "px -" + (y * 10) + "px;\"></div>";
        }
        if (icon[7] > 0) {
            x = (icon[7] - 1);
            y = 0;
            html += "<div class=\"icon_corner icon_down\" style=\"background-position: -" + (x * 10) + "px -" + (y * 10) + "px;\"></div>";
        }
        if (icon[8] > 0) {
            x = (icon[8] - 1);
            y = 0;
            html += "<div class=\"icon_corner icon_left\" style=\"background-position: -" + (x * 10) + "px -" + (y * 10) + "px;\"></div>";
        }
        if (icon[9] > 0) {
            ic = icon[9] - 1;
            x = (ic % 7);
            y = (ic - x) / 7;
            html += "<div class=\"icon_spell\" style=\"background-position: -" + (x * 20) + "px -" + (y * 12) + "px;\"></div>";
        }
        html += "</div>";
    }
    return html;
}

function load_data() {
    if (debug) console.log("load_data");
    $.getJSON("/chrplan/daoc.json"/*?v=" + v*/, function (data) {
        daoc = data;

        $.get("/chrplan/icons.txt", function (iconstxt) {
            let temp = iconstxt.split("|");
            let temp0 = temp[0].split("\n");
            $.each(temp0, function (i, el) {
                if (el !== "") {
                    let elspl = el.split(",");
                    let icon = [elspl[0] * 1, 0, 0, 0, 0, 0, 0, 0, 0, 0];

                    let len = elspl.length;
                    for (let i2 = 1; i2 < len; i2++) {
                        let eln = elspl[i2];
                        let elnt = (eln.slice(1) * 1) + 1;
                        switch (eln.charAt(0)) {
                            case "B": icon[1] = elnt; break;
                            case "S": icon[9] = elnt; break;
                            case "0": icon[2] = elnt; break;
                            case "1": icon[3] = elnt; break;
                            case "2": icon[4] = elnt; break;
                            case "3": icon[5] = elnt; break;
                            case "4": icon[6] = elnt; break;
                            case "5": icon[7] = elnt; break;
                            case "6": icon[8] = elnt; break;
                        }
                    }
                    icons[i + 1] = icon;
                }
            });
            //console.log(icons);

            let temp1 = temp[1].split("\n");
            $.each(temp1, function (i, el) {
                if (el !== "") {
                    spells[i + 1] = el * 1;
                }
            });
            //console.log(spells);

            let temp2 = temp[2].split("\n");
            $.each(temp2, function (i, el) {
                if (el !== "") {
                    styles[i + 1] = el * 1;
                }
            });
            //console.log(styles);


            $.getJSON("/chrplan/charplan.json?v=" + v, function (data) {
                json = data;

                $.getJSON("/chrplan/charplan_cl.json?v=" + v, function (datacl) {
                    jsoncl = datacl;

                    if (p === "s")
                        build_specs();
                    else if (p == "r")
                        build_ras();
                    else
                        build_misc();

                    //select classes: //now hardcoded in html
                    //$.each(daoc.classes, function (i, v) {
                    //    $("#classes").append($("<option>", {
                    //        value: i,
                    //        text: i
                    //    }));
                    //});

                    $("#classes option[value='" + c + "']").prop("selected", true);
                    $("#classes").change(function () {
                        let old = c;
                        c = $("#classes").val();

                        if (old !== c) {
                            specs_level = {};
                            ras_level = {};
                            cls_trained = {};
                            getcookie();

                            if (json !== null) {
                                if (p === "s")
                                    build_specs();
                                else if (p === "r")
                                    build_ras();
                                else
                                    build_misc();
                            }
                        }

                        $("#races").empty();
                        $.each(daoc.classes[c].races, function (i, v) {
                            $("#races").append($("<option>", {
                                value: v,
                                text: v,
                                class: "neutral-list"
                            }));
                        });
                        $("#races option[value='" + r + "']").prop("selected", true);


                        $("#races").change();
                    });
                    init_pickers();
                    $("#classes").change();

                    $("#races").change(function () {
                        r = $("#races").val();
                        update_url();
                    });
                });
            });
        });
    });
}

function update_rr(dir) {
    if (debug) console.log("update_rr", dir);
    let temp = rr.split("L");
    let val = (temp[0] * 10) + temp[1] * 1;
    if (dir !== undefined) {
        val += dir;
        if (val > 150)
            val = 150;
        if (val < 10)
            val = 10;
    }
    let left = Math.floor(val / 10);
    let result = left + "L" + (val - (left * 10));
    if (isNaN(left)) {
        result = "5L0";
        val = 40;
    }
    rr = result;
    $("#rr").val(rr);

    if (p === "r") {
        points_total = val - 10;
        update_totalpoints();
    }

    update_url();
};

function update_speclevel(spec_id, newlevel) {
    if (debug) console.log("update_speclevel", spec_id, newlevel);
    if (newlevel > Math.floor(l))
        newlevel = Math.floor(l);
    if (newlevel < 1)
        newlevel = 1;

    //auto find max level, or discard if too high
    let remainingpoints = calc_remainingpoints(spec_id, newlevel);
    if (remainingpoints < 0 && newlevel > specs_level[spec_id]) {
        let found = false;
        for (let nl = newlevel; nl > 1; nl--) {
            if (calc_remainingpoints(spec_id, nl) >= 0) {
                found = true;
                newlevel = nl;
                break;
            }
        }
        if (!found)
            return;
    }

    specs_level[spec_id] = newlevel;

    let autotrain = 0;
    if (specs[spec_id] !== undefined) {
        if (specs[spec_id].autotrain === true) {
            let free = Math.floor(l / 4);
            for (let i = 2; i <= free; i++) {
                autotrain += i;
            }
        }
    }

    let points = -1;
    for (let i = 1; i <= newlevel; i++) {
        points += i;
    }
    points -= autotrain;
    if (points < 0) points = 0;

    $("#spec_level_" + spec_id).html(specs_level[spec_id]);
    if (specs[spec_id] !== undefined) {
        $("#spec_points_" + spec_id).html(points + (specs[spec_id].autotrain === true ? "&nbsp;&nbsp;&nbsp;(Autotrain)" : ""));
    }

    $(".spec_skill[data-base='0'][data-spec='" + spec_id + "']").filter(function () {
        return $(this).data("level") * 1 <= specs_level[spec_id];
    }).addClass("selected");
    $(".spec_skill[data-base='0'][data-spec='" + spec_id + "']").filter(function () {
        return $(this).data("level") * 1 > specs_level[spec_id];
    }).removeClass("selected");

    update_totalpoints();
    update_url();
}

function update_ralevel(ra_id, newlevel) {
    if (debug) console.log("update_ralevel", ra_id, newlevel);
    if (newlevel < 0)
        newlevel = 0;

    //auto find max level, or discard if too high
    let remainingpoints = calc_remainingpoints(ra_id, newlevel);
    if (debug) console.log("remainingpoints", remainingpoints);
    if (remainingpoints < 0 && newlevel > ras_level[ra_id]) {
        let found = false;
        for (let nl = newlevel; nl > 0; nl--) {
            if (calc_remainingpoints(ra_id, nl) >= 0) {
                found = true;
                newlevel = nl;
                break;
            }
        }
        if (!found)
            return;
    }

    ras_level[ra_id] = newlevel;

    $(".spec_rainfo[data-id='" + ra_id + "']").filter(function () {
        return $(this).data("level") * 1 <= ras_level[ra_id];
    }).addClass("selected");
    $(".spec_rainfo[data-id='" + ra_id + "']").filter(function () {
        return $(this).data("level") * 1 > ras_level[ra_id];
    }).removeClass("selected");

    update_totalpoints();
    update_url();
}

function update_clslevel() {
    cls_trained = {};
    $(".cl_icon").removeClass("active");
    update_remainingpoints();
    update_url();
}

function update_totalpoints() {
    if (debug) console.log("update_totalpoints");
    if (p === "s") {
        let points = 0;
        for (let i = 2; i <= l; i += 0.5) {
            let round = (Math.round(i) === i);
            if (i < 40 && !round)
                continue;
            points += Math.floor(Math.floor(i) * (i > 5 ? points_multiplier : 1) * (round ? 1 : 0.5));
        }
        points_total = points;
        $("#points_total").html(points_total);
    } else if (p === "r") {
        $("#points_total").html(points_total);
    } else if (p === "m") {
        $("#points_total").html(points_total);
    }
    update_remainingpoints();
}

function calc_remainingpoints(spec_id, newlevel) {
    if (debug) console.log("calc_remaningpoints", spec_id, newlevel);
    let total = 0;
    if (p === "s") {
        total = points_total;
        if (Object.keys(specs) <= 0)
            return;
        $.each(specs_level, function (id, level) {
            if (id * 1 === spec_id * 1)
                level = newlevel;
            if (level <= 1)
                return;

            let autotrain = 0;
            if (specs[id].autotrain === true) {
                let free = Math.floor(l / 4);
                for (let i = 2; i <= free; i++) {
                    autotrain += i;
                }
            }

            let points = -1;
            for (let i = 1; i <= level; i++) {
                points += i;
            }
            points -= autotrain;
            if (points < 0) points = 0;

            total -= points;
        });
    } else if (p === "r") {
        total = points_total;
        if (Object.keys(ras) <= 0)
            return;
        $.each(ras_level, function (id, level) {
            if (id * 1 === spec_id * 1)
                level = newlevel;
            if (level <= 0)
                return;

            let points = 0;
            for (let i = 0; i < level; i++) {
                points += ras[id].levels[i].cost;
            }

            if (points < 0) points = 0;

            total -= points;
        });
    } else if (p === "m") {
        total = points_total;
        if (Object.keys(cls) <= 0)
            return;
        $.each(cls_trained, function (index, cl) {
            total -= Object.keys(cls_trained[index]).length;
        });
    }
    return total;
}

function update_remainingpoints() {
    if (debug) console.log("update_remaningpoints");
    if (p === "s") {
        points_remain = calc_remainingpoints();
        $("#points_remain").html(points_remain);
        $("#points_used").html(points_total - points_remain);
    } else if (p === "r") {
        points_remain = calc_remainingpoints();
        $("#points_remain").html(points_remain);
        $("#points_used").html(points_total - points_remain);
    } else if (p === "m") {
        points_remain = calc_remainingpoints();
        $("#points_remain").html(points_remain);
        $("#points_used").html(points_total - points_remain);
    }
}

function update_url() {
    if (!ready) return;
    if (debug) console.log("update_url", p);
    let params = "c=" + c + "&l=" + l + "&r=" + r + "&rr=" + rr + "&p=" + p;
    $.each(specs_level, function (i, level) {
        if (level <= 1)
            return;
        params += "&s" + i + "=" + level;
    });
    $.each(ras_level, function (i, level) {
        if (level <= 0)
            return;
        params += "&a" + i + "=" + level;
    });
    $.each(cls_trained, function (name, cl_line) {
        let total = 0;
        $.each(cl_line, function (key, cl) {
            let spl = key.split("_");
            let col = spl[0] * 1;
            let row = spl[1] * 1;
            let level = ((row - 1) * 6) + (col - 1);
            let bit = 1 << level;
            total |= bit;
        });
        if (debug) console.log("cl[" + name + "].bits", total.toString(2));
        if (total > 0) {
            params += "&cl" + cls[name].id + "=" + total.toString(36);
        }
    });
    let newurl = window.location.protocol + "//" + window.location.host + window.location.pathname + "?" + params;
    update_permalinks(params, newurl);
    if (ready && window.history.pushState && document.location.href !== newurl) {
        window.history.replaceState({ path: newurl }, "", newurl);
    }
    if (Cookies) {
        Cookies.set("charplan_" + c, params);
    }
}

// Real links: the tabs and Share carry the current build as a permalink.
// Refreshed by update_url() on every edit AND once after the initial build
// (update_url only fires on edits, so a fresh load would leave the tabs
// href-less). No replaceState/cookie side effects here: on load, a shared
// link must not overwrite the visitor's own saved build.
// Hrefs keep the FULL param set - a bare ?p=r would trigger the cookie
// fallback and load a different build.
function update_permalinks(params, newurl) {
    if (params === undefined) {
        params = "c=" + c + "&l=" + l + "&r=" + r + "&rr=" + rr + "&p=" + p;
        $.each(specs_level, function (i, level) {
            if (level <= 1)
                return;
            params += "&s" + i + "=" + level;
        });
        $.each(ras_level, function (i, level) {
            if (level <= 0)
                return;
            params += "&a" + i + "=" + level;
        });
        $.each(cls_trained, function (name, cl_line) {
            let total = 0;
            $.each(cl_line, function (key, cl) {
                let spl = key.split("_");
                let level = (((spl[1] * 1) - 1) * 6) + ((spl[0] * 1) - 1);
                total |= 1 << level;
            });
            if (total > 0) {
                params += "&cl" + cls[name].id + "=" + total.toString(36);
            }
        });
        newurl = window.location.protocol + "//" + window.location.host + window.location.pathname + "?" + params;
    }
    $("#directurl").val(newurl);
    $("#skills").attr("href", "?" + params.replace("&p=" + p, "&p=s"));
    $("#ras").attr("href", "?" + params.replace("&p=" + p, "&p=r"));
    $("#misc").attr("href", "?" + params.replace("&p=" + p, "&p=m"));
    $("#directurlcc").attr("href", newurl);
}

function getparam(key) {
    var result = null, tmp = [];
    var items = location.search.substr(1).split("&");
    for (let index = 0; index < items.length; index++) {
        tmp = items[index].split("=");
        if (tmp[0] === key)
            result = decodeURIComponent(tmp[1]);
    }
    return result;
}

var messagebox_timer = null;
function messagebox_show(msg) {
    $("#messagebox").html(msg);
    $("#hidder").show();
    if (messagebox_timer !== null)
        clearTimeout(messagebox_timer);
    messagebox_timer = setTimeout(function () {
        messagebox_hide();
    }, 5000);
}

function messagebox_hide() {
    $("#hidder").fadeOut();
}

function getcookie() {
    if (window.history.pushState) {
        let params = Cookies.get("charplan_" + c);
        if (debug) console.log("getcookie", params);
        let newurl = "";
        if (params !== undefined) {
            newurl = window.location.protocol + "//" + window.location.host + window.location.pathname + "?" + params;
        } else {
            newurl = window.location.protocol + "//" + window.location.host + window.location.pathname + "?c=" + c + "&l=50&rr=5L0&&p=s";//default
        }
        window.history.replaceState({ path: newurl }, "", newurl);
    }
}


/* ---------- Custom class/race pickers ---------- */

const PICKER_BTN_MAP = {
    "class-popover": "class-picker",
    "race-popover":  "race-picker",
    "level-popover": "level-picker"
};

function init_pickers() {
    const classPop = document.getElementById("class-popover");
    if (!classPop || typeof daoc === "undefined" || !daoc.classes) return;

    const grouped = { 1: [], 2: [], 3: [] };
    Object.keys(daoc.classes).forEach(function (name) {
        const realm = daoc.classes[name].realm;
        if (grouped[realm]) grouped[realm].push(name);
    });
    [1, 2, 3].forEach(function (realmId) {
        grouped[realmId].sort();
        const col = classPop.querySelector('[data-realm="' + realmId + '"] .popover-col-list');
        if (!col) return;
        col.innerHTML = "";
        grouped[realmId].forEach(function (name) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "popover-item";
            btn.dataset.value = name;
            btn.textContent = name;
            btn.addEventListener("click", function () {
                $("#classes").val(name).change();
                closePopover("class-popover");
            });
            col.appendChild(btn);
        });
    });

    // Level popover - whole levels 50→1 (grid), then half levels 49.5→40.5
    const wholeList = document.querySelector("#level-popover .popover-level-whole-list");
    const halfList  = document.querySelector("#level-popover .popover-level-half-list");
    if (wholeList) {
        wholeList.innerHTML = "";
        for (let i = 50; i >= 1; i--) {
            const b = document.createElement("button");
            b.type = "button";
            b.className = "popover-level-item";
            b.dataset.value = String(i);
            b.textContent = String(i);
            b.addEventListener("click", function () {
                $("#levels").val(i).change();
                closePopover("level-popover");
            });
            wholeList.appendChild(b);
        }
    }
    if (halfList) {
        halfList.innerHTML = "";
        for (let i = 49.5; i >= 40.5; i -= 1) {
            const val = i;
            const b = document.createElement("button");
            b.type = "button";
            b.className = "popover-level-item half";
            b.dataset.value = String(val);
            b.textContent = String(val);
            b.addEventListener("click", function () {
                $("#levels").val(val).change();
                closePopover("level-popover");
            });
            halfList.appendChild(b);
        }
    }

    const classBtn = document.getElementById("class-picker");
    const raceBtn = document.getElementById("race-picker");
    const levelBtn = document.getElementById("level-picker");
    if (classBtn) classBtn.addEventListener("click", function (e) {
        e.stopPropagation();
        togglePopover("class-popover", this);
    });
    if (raceBtn) raceBtn.addEventListener("click", function (e) {
        e.stopPropagation();
        togglePopover("race-popover", this);
    });
    if (levelBtn) levelBtn.addEventListener("click", function (e) {
        e.stopPropagation();
        togglePopover("level-popover", this);
    });

    document.addEventListener("click", function (e) {
        if (!e.target.closest(".picker-popover") && !e.target.closest(".picker-btn")) {
            closeAllPopovers();
        }
    });
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") closeAllPopovers();
    });

    $("#classes").on("change.picker", sync_class_picker);
    $("#races").on("change.picker", sync_race_picker);
    $("#levels").on("change.picker", sync_level_picker);

    // Initial sync (level already triggered its change before init)
    sync_level_picker();
}

function closeAllPopovers() {
    Object.keys(PICKER_BTN_MAP).forEach(closePopover);
}

function togglePopover(id, btn) {
    const pop = document.getElementById(id);
    if (!pop) return;
    const willOpen = pop.hidden;
    document.querySelectorAll(".picker-popover").forEach(function (p) { p.hidden = true; });
    document.querySelectorAll(".picker-btn").forEach(function (b) {
        b.classList.remove("open");
        b.setAttribute("aria-expanded", "false");
    });
    if (willOpen) {
        pop.hidden = false;
        if (btn) {
            btn.classList.add("open");
            btn.setAttribute("aria-expanded", "true");
        }
    }
}

function closePopover(id) {
    const pop = document.getElementById(id);
    if (pop) pop.hidden = true;
    const btnId = PICKER_BTN_MAP[id];
    const btn = btnId && document.getElementById(btnId);
    if (btn) {
        btn.classList.remove("open");
        btn.setAttribute("aria-expanded", "false");
    }
}

function sync_class_picker() {
    const val = $("#classes").val();
    const btn = document.getElementById("class-picker");
    if (!btn) return;
    const label = btn.querySelector(".picker-label");
    if (label) label.textContent = val || "—";
    const realm = (val && daoc.classes[val] && daoc.classes[val].realm) || 0;
    btn.dataset.realm = realm;
    document.querySelectorAll("#class-popover .popover-item").forEach(function (it) {
        it.classList.toggle("selected", it.dataset.value === val);
    });
}

function sync_race_picker() {
    const val = $("#races").val();
    const btn = document.getElementById("race-picker");
    if (!btn) return;
    const label = btn.querySelector(".picker-label");
    if (label) label.textContent = val || "—";
    const cls = $("#classes").val();
    const realm = (cls && daoc.classes[cls] && daoc.classes[cls].realm) || 0;
    const pop = document.getElementById("race-popover");
    if (pop) pop.dataset.realm = realm;
    const list = pop && pop.querySelector(".popover-race-list");
    if (!list) return;
    list.innerHTML = "";
    const races = (cls && daoc.classes[cls] && daoc.classes[cls].races) || [];
    races.forEach(function (race) {
        const ib = document.createElement("button");
        ib.type = "button";
        ib.className = "popover-item" + (race === val ? " selected" : "");
        ib.dataset.value = race;
        ib.textContent = race;
        ib.addEventListener("click", function () {
            $("#races").val(race).change();
            closePopover("race-popover");
        });
        list.appendChild(ib);
    });
}

function sync_level_picker() {
    const val = $("#levels").val();
    const btn = document.getElementById("level-picker");
    if (!btn) return;
    const label = btn.querySelector(".picker-label");
    if (label) label.textContent = val || "—";
    document.querySelectorAll("#level-popover .popover-level-item").forEach(function (it) {
        it.classList.toggle("selected", it.dataset.value === String(val));
    });
}
