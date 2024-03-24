using System;
using System.Linq;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(Terminal))]
public class TerminalPatch {
    private static Terminal _instance;
    private static bool _addedTerminalCommands;

    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    private static void InitializeTerminal (ref Terminal __instance) {
        _instance = __instance;
        EditTerminalDisplay();
    }

    [HarmonyPostfix]
    [HarmonyPatch("BeginUsingTerminal")]
    private static void DoubleCheckTerminalEdits (ref Terminal __instance) {
        EditTerminalDisplay();
    }

    [HarmonyPrefix]
    [HarmonyPatch("ParsePlayerSentence")]
    public static bool ParseTerminalCommand (ref TerminalNode __result, ref Terminal __instance) {
        string command = __instance.screenText.text[^__instance.textAdded..].ToLower();

        TerminalNode resultNode = ParseVocabularyCommands(command);
        if (resultNode != null) {
            __result = resultNode;
            return false;
        }

        if (!PunishmentManager.Instance.StringIsLegal(command)) {
            __instance.screenText.text = "";
            return false;
        }

        return true;
    }

    private static void EditTerminalDisplay () {
        if (_addedTerminalCommands) return;

        TerminalNode startNode = _instance.terminalNodes.specialNodes.First(n => n.name.Equals("Start"));
        if (startNode != null) {
            const string indexString = "Type \"Help\" for a list of commands.";
            int index = startNode.displayText.IndexOf(indexString, StringComparison.Ordinal) + indexString.Length;
            startNode.displayText = startNode.displayText
                .Insert(index, "\n\n[LethalVocabulary]\nType \"vocabulary\" for a list of commands.");
        }
        else {
            Plugin.Console.LogWarning("Failed to find \"Start\" terminal node");
        }

        TerminalNode helpNode = _instance.terminalNodes.specialNodes.First(n => n.name.Equals("HelpCommands"));
        if (helpNode != null) {
            const string indexString = "To see the list of other commands";
            int index = helpNode.displayText.IndexOf(indexString, StringComparison.Ordinal) + indexString.Length;
            helpNode.displayText = helpNode.displayText
                .Insert(index, "\n\n>VOCABULARY\nTo see LethalVocabulary's commands.");
        }
        else {
            Plugin.Console.LogWarning("Failed to find \"Help\" terminal node");
        }

        _addedTerminalCommands = true;
    }

    #region Terminal Nodes

    private const string HomeNodeName = "LV_Home";
    private const string PunishmentNodeName = "LV_Punishment";
    private const string CategoriesPerMoonNodeName = "LV_CategoriesPerMoon";

    private static readonly string Header =
        $"LethalVocabulary Settings\n" +
        $"-------------------------\n";

    private static TerminalNode ParseVocabularyCommands (string command) {
        string[] args = command.Split(" ");

        if ("vocabulary".StartsWith(args[0])) return CreateHomeNode();

        if (!StartOfRound.Instance.localPlayerController.IsHost ||
            PunishmentManager.Instance.MoonInProgress.Value) return null;

        switch (_instance.currentNode.name) {
            case HomeNodeName:
                switch (args[0]) {
                    case "1":
                        return CreatePunishmentNode();
                    case "2":
                        return CreateCategoriesPerMoonNode(true);
                    case "3":
                        return CreateCategoriesPerMoonNode(false);
                    case "4":
                        PunishmentManager.Instance.SetCategoryHintsServerRpc(
                            !PunishmentManager.Instance.sharedCategoryHints,
                            PunishmentManager.Instance.privateCategoryHints);
                        return CreateHomeNode();
                    case "5":
                        PunishmentManager.Instance.SetCategoryHintsServerRpc(
                            PunishmentManager.Instance.sharedCategoryHints,
                            !PunishmentManager.Instance.privateCategoryHints);
                        return CreateHomeNode();
                    case "6":
                        PunishmentManager.Instance.SetForcedCategoryStatusServerRpc(
                            Category.CurseWords, !PunishmentManager.Instance.IsPunishCurseWords());
                        return CreateHomeNode();
                }

                return null;
            case PunishmentNodeName:
                if (!int.TryParse(args[0], out int index)) return null;
                if (index < 0 || index >= Enum.GetValues(typeof(Punishment)).Length) return null;
                PunishmentManager.Instance.SetActivePunishmentServerRpc(
                    new FixedString64Bytes(((Punishment)index).ToString()));
                return CreateHomeNode();
            case CategoriesPerMoonNodeName:
                if (!int.TryParse(args[0], out int categoriesPerMoon)) return null;
                if (categoriesPerMoon < 0) return null;
                int shared = _instance.currentNode.displayText.Contains("Shared") ? categoriesPerMoon : -1;
                int @private = _instance.currentNode.displayText.Contains("Private") ? categoriesPerMoon : -1;
                PunishmentManager.Instance.SetCategoriesPerMoonServerRpc(shared, @private);
                return CreateHomeNode();
            default:
                return null;
        }
    }

    private static TerminalNode CreateHomeNode () {
        TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
        node.name = HomeNodeName;
        node.clearPreviousText = true;

        node.displayText =
            Header +
            (!StartOfRound.Instance.localPlayerController.IsHost ? "Only the host can change settings.\n" : "") +
            (PunishmentManager.Instance.MoonInProgress.Value ? "Settings cannot be changed mid-moon.\n" : "") + "\n" +
            $"(1) Punishment: {PunishmentManager.Instance.activePunishment.ToString()}\n" +
            $"(2) Shared Categories Per Moon: {PunishmentManager.Instance.sharedCategoriesPerMoon}\n" +
            $"(3) Private Categories Per Moon: {PunishmentManager.Instance.privateCategoriesPerMoon}\n" +
            $"(4) Shared Category Hints: {PunishmentManager.Instance.sharedCategoryHints}\n" +
            $"(5) Private Category Hints: {PunishmentManager.Instance.privateCategoryHints}\n" +
            $"(6) Punish Curse Words: {PunishmentManager.Instance.IsPunishCurseWords()}\n\n" +
            "Changes are NOT saved to your config.\n" +
            "Enter the setting number to edit.\n";

        return node;
    }

    private static TerminalNode CreatePunishmentNode () {
        TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
        node.name = PunishmentNodeName;
        node.clearPreviousText = true;

        string availablePunishments = Enum.GetValues(typeof(Punishment)).Cast<Punishment>()
            .Aggregate("", (current, punishment) => current + $"({(int)punishment}) {punishment.ToString()}\n")
            .Trim();

        node.displayText =
            Header +
            $"Punishment: {PunishmentManager.Instance.activePunishment.ToString()}\n\n" +
            $"Available Punishments:\n{availablePunishments}\n\n" +
            "Enter a punishment name to activate it.\n";

        return node;
    }

    private static TerminalNode CreateCategoriesPerMoonNode (bool setSharedCategories) {
        TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
        node.name = CategoriesPerMoonNodeName;
        node.clearPreviousText = true;

        string categoryType = setSharedCategories ? "Shared" : "Private";
        int currentValue = setSharedCategories
            ? PunishmentManager.Instance.sharedCategoriesPerMoon
            : PunishmentManager.Instance.privateCategoriesPerMoon;

        node.displayText =
            Header +
            $"{categoryType} Categories Per Moon: {currentValue}\n\n" +
            $"Enter the number of {categoryType} categories that will be selected per moon.\n" +
            $"Enter 0 to disable {categoryType} categories.\n";

        return node;
    }

    #endregion
}