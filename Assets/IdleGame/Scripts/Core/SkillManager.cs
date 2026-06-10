using System;
using System.Collections.Generic;
using UnityEngine;
using IdleTime.Core;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [SerializeField] SkillTreeDefinition[] skillTrees;

    public static event Action OnSkillsChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public SkillTreeDefinition GetTree(PlayerClass playerClass)
    {
        foreach (var tree in skillTrees)
            if (tree.playerClass == playerClass) return tree;
        return null;
    }

    public IEnumerable<SkillTreeDefinition> GetAccessibleTrees(CharacterData character)
    {
        foreach (var tree in skillTrees)
            if (character.unlockedClasses.Contains(tree.playerClass))
                yield return tree;
    }

    public bool CanUnlock(SkillNodeEntry node, CharacterData character)
    {
        if (character.skills.availableSkillPoints <= 0) return false;
        if (character.skills.GetLevel(node.skill) >= node.skill.maxLevel) return false;
        foreach (var prereq in node.prerequisites)
            if (!character.skills.IsUnlocked(prereq)) return false;
        return true;
    }

    public bool TryUnlock(SkillNodeEntry node, CharacterData character)
    {
        if (!CanUnlock(node, character)) return false;
        character.skills.TryUnlock(node.skill);
        RecomputeSkillBonuses(character);
        OnSkillsChanged?.Invoke();
        // Mirror EquipmentManager: a skill can change Attack/Defense/MaxHP, so the
        // stats panel (and anything else on OnStatsChanged) refreshes immediately.
        PlayerManager.Instance?.NotifyStatsChanged();
        return true;
    }

    // Call this when a character advances to a new class for the first time.
    public void UnlockClass(CharacterData character, PlayerClass newClass)
    {
        if (character.unlockedClasses.Contains(newClass)) return;
        character.unlockedClasses.Add(newClass);
        OnSkillsChanged?.Invoke();
    }

    public void GainSkillPoints(CharacterData character, int amount)
    {
        character.skills.availableSkillPoints += amount;
        OnSkillsChanged?.Invoke();
    }

    public void RecomputeSkillBonuses(CharacterData character)
    {
        character.skillBonusAttack = 0;
        character.skillBonusDefense = 0;
        character.skillBonusMaxHP = 0;
        character.skillBonusMaxMP = 0;
        character.skillBonusAccuracy = 0;
        character.skillBonusStr = 0;
        character.skillBonusDex = 0;
        character.skillBonusWis = 0;
        character.skillBonusLuk = 0;

        foreach (var tree in GetAccessibleTrees(character))
        {
            foreach (var node in tree.nodes)
            {
                int lvl = character.skills.GetLevel(node.skill);
                if (lvl <= 0) continue;
                float val = node.skill.effectValuePerLevel * lvl;
                switch (node.skill.effectType)
                {
                    case SkillEffectType.BonusAttack:   character.skillBonusAttack   += Mathf.RoundToInt(val); break;
                    case SkillEffectType.BonusDefense:  character.skillBonusDefense  += Mathf.RoundToInt(val); break;
                    case SkillEffectType.BonusMaxHP:    character.skillBonusMaxHP    += Mathf.RoundToInt(val); break;
                    case SkillEffectType.BonusMaxMP:    character.skillBonusMaxMP    += Mathf.RoundToInt(val); break;
                    case SkillEffectType.BonusAccuracy: character.skillBonusAccuracy += Mathf.RoundToInt(val); break;
                    // Primary-stat buffs. These cascade: Attack reads damageStat and Accuracy
                    // reads accuracyStat, so e.g. +Str raises a Fighter's Attack automatically.
                    case SkillEffectType.BonusStr:      character.skillBonusStr      += Mathf.RoundToInt(val); break;
                    case SkillEffectType.BonusDex:      character.skillBonusDex      += Mathf.RoundToInt(val); break;
                    case SkillEffectType.BonusWis:      character.skillBonusWis      += Mathf.RoundToInt(val); break;
                    case SkillEffectType.BonusLuk:      character.skillBonusLuk      += Mathf.RoundToInt(val); break;
                }
            }
        }

        // Skill changes can't currently lower a max, but keep vitals in range so a
        // future debuff/respec path can't strand currentHP above MaxHP.
        character.ClampVitals();
    }
}
