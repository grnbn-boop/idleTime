using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleTime.Core;

public class SkillTreeUI : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] TextMeshProUGUI skillPointsText;

    [Header("Tabs")]
    [SerializeField] RectTransform tabContainer;
    [SerializeField] Button tabButtonPrefab;

    [Header("Content")]
    // LinesContainer must be a LOWER sibling than NodesContainer so lines render behind nodes.
    [SerializeField] RectTransform linesContainer;
    [SerializeField] RectTransform nodesContainer;

    [Header("Prefabs")]
    [SerializeField] SkillNodeUI nodeUIPrefab;
    [SerializeField] Image lineImagePrefab;

    [Header("Layout")]
    [SerializeField] float nodeSize = 80f;

    private readonly List<SkillNodeUI> _spawnedNodes = new();
    private readonly List<Image>       _spawnedLines = new();
    private SkillTreeDefinition _currentTree;

    void OnEnable()
    {
        Refresh();
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.OnActiveCharacterChanged += Refresh;
            PlayerManager.Instance.OnStatsChanged           += Refresh;
        }
        SkillManager.OnSkillsChanged += Refresh;
    }

    void OnDisable()
    {
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.OnActiveCharacterChanged -= Refresh;
            PlayerManager.Instance.OnStatsChanged           -= Refresh;
        }
        SkillManager.OnSkillsChanged -= Refresh;
    }

    void Refresh()
    {
        if (PlayerManager.Instance == null) return;
        var character = PlayerManager.Instance.ActiveCharacter;
        if (character == null) return;

        if (skillPointsText != null)
            skillPointsText.text = $"SP: {character.skills.availableSkillPoints}";
        else
            Debug.LogWarning("[SkillTreeUI] skillPointsText is not assigned.", this);

        if (SkillManager.Instance == null) return;

        RebuildTabs(character);

        // If the current tree is no longer accessible, reset it.
        if (_currentTree != null && !character.unlockedClasses.Contains(_currentTree.playerClass))
            _currentTree = null;

        ShowTree(_currentTree ?? GetDefaultTree(character), character);
    }

    void RebuildTabs(CharacterData character)
    {
        foreach (Transform t in tabContainer) Destroy(t.gameObject);

        foreach (var tree in SkillManager.Instance.GetAccessibleTrees(character))
        {
            var captured = tree;
            var tab = Instantiate(tabButtonPrefab, tabContainer);
            tab.GetComponentInChildren<TextMeshProUGUI>().text = tree.playerClass.className;
            tab.onClick.AddListener(() =>
            {
                _currentTree = captured;
                ShowTree(captured, PlayerManager.Instance.ActiveCharacter);
            });
        }
    }

    SkillTreeDefinition GetDefaultTree(CharacterData character) =>
        SkillManager.Instance.GetTree(character.playerClass)
        ?? SkillManager.Instance.GetAccessibleTrees(character).FirstOrDefault();

    void ShowTree(SkillTreeDefinition tree, CharacterData character)
    {
        _currentTree = tree;
        ClearSpawned();

        if (tree == null) return;

        var nodeBySkill = new Dictionary<SkillDefinition, SkillNodeUI>();

        foreach (var entry in tree.nodes)
        {
            if (entry.skill == null) continue;

            var node = Instantiate(nodeUIPrefab, nodesContainer);
            node.Setup(entry, character, OnNodeClicked);

            var rt        = node.GetComponent<RectTransform>();
            rt.anchorMin  = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.sizeDelta  = new Vector2(nodeSize, nodeSize);
            rt.anchoredPosition = entry.position;

            nodeBySkill[entry.skill] = node;
            _spawnedNodes.Add(node);
        }

        // Draw lines from each prerequisite skill to the node that requires it.
        foreach (var entry in tree.nodes)
        {
            if (entry.skill == null) continue;
            foreach (var prereq in entry.prerequisites)
            {
                if (prereq == null) continue;
                if (!nodeBySkill.TryGetValue(prereq, out var fromNode)) continue;
                if (!nodeBySkill.TryGetValue(entry.skill, out var toNode)) continue;
                DrawLine(fromNode.GetComponent<RectTransform>().anchoredPosition,
                         toNode.GetComponent<RectTransform>().anchoredPosition);
            }
        }
    }

    void DrawLine(Vector2 from, Vector2 to)
    {
        Vector2 dir  = to - from;
        float   dist = dir.magnitude;
        if (dist < 1f) return;

        var line = Instantiate(lineImagePrefab, linesContainer);
        var rt   = line.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(dist, 4f);
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        _spawnedLines.Add(line);
    }

    void OnNodeClicked(SkillNodeEntry entry)
    {
        var character = PlayerManager.Instance?.ActiveCharacter;
        if (character == null) return;

        if (SkillManager.Instance.TryUnlock(entry, character))
        {
            // Refresh all node visuals so newly-available skills light up.
            foreach (var node in _spawnedNodes)
                node.Refresh(character);
            skillPointsText.text = $"SP: {character.skills.availableSkillPoints}";
        }
    }

    void ClearSpawned()
    {
        foreach (var n in _spawnedNodes) if (n) Destroy(n.gameObject);
        foreach (var l in _spawnedLines) if (l) Destroy(l.gameObject);
        _spawnedNodes.Clear();
        _spawnedLines.Clear();
    }
}
