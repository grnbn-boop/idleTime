using System;
using System.Collections.Generic;
using IdleTime.Core;
using IdleTime.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace IdleTime.Interactions
{
    public class ClassTrainerNpc : NpcDialogue
    {
        [Header("Class Trainer")]
        [FormerlySerializedAs("debugClassChoices")]
        [SerializeField] private PlayerClass[] classChoices;
        [SerializeField] private bool lockAfterClassPicked;
        [SerializeField] private PlayerClass startingClass;
        [SerializeField] private bool saveImmediately = true;

        [Header("Class Options")]
        [SerializeField] private Vector2 optionsLocalOffset = new Vector2(0f, 0.42f);
        [SerializeField] private float optionSpacing = 0.16f;
        [SerializeField] private Vector2 optionBoxSize = new Vector2(1.5f, 0.18f);
        [SerializeField] private TMP_FontAsset optionFont;
        [SerializeField] private float optionFontSize = 2.5f;
        [SerializeField] private int optionSortingOrder = 31;

        private readonly List<OptionView> optionViews = new();
        private Transform optionsRoot;
        private Camera cachedCamera;
        private ClickToMove2D playerMovement;
        private bool optionsVisible;

        protected override void Awake()
        {
            base.Awake();
            cachedCamera = Camera.main;
            playerMovement = FindAnyObjectByType<ClickToMove2D>();
            HideOptions();
        }

        protected override void Update()
        {
            if (optionsVisible)
            {
                EnsureOptions();
                if (TryHandleOptionClick())
                    return;
            }

            base.Update();
        }

        public override void Interact()
        {
            if (optionsVisible)
                return;

            if (lines == null || lines.Length == 0)
            {
                ShowOptions();
                return;
            }

            base.Interact();
        }

        protected override void OnDialogueFinished()
        {
            ShowOptions();
        }

        private void ShowOptions()
        {
            var character = PlayerManager.Instance?.ActiveCharacter;
            if (character == null)
            {
                Say("No active character found.");
                return;
            }

            if (lockAfterClassPicked && HasAlreadyPickedClass(character))
            {
                Say($"You are already a {character.ClassName}.");
                return;
            }

            EnsureOptions();
            optionsVisible = optionViews.Count > 0;
            if (optionsRoot != null)
                optionsRoot.gameObject.SetActive(optionsVisible);
        }

        private void HideOptions()
        {
            optionsVisible = false;
            if (optionsRoot != null)
                optionsRoot.gameObject.SetActive(false);
        }

        private void EnsureOptions()
        {
            if (optionsRoot == null)
            {
                GameObject root = new GameObject("ClassOptions");
                optionsRoot = root.transform;
                optionsRoot.SetParent(transform, false);
            }

            Vector3 anchor = dialogueText != null
                ? dialogueText.transform.localPosition
                : new Vector3(0f, 0.55f, 0f);
            optionsRoot.localPosition = anchor + (Vector3)optionsLocalOffset;
            optionsRoot.localRotation = Quaternion.identity;
            optionsRoot.localScale = dialogueText != null
                ? dialogueText.transform.localScale
                : new Vector3(0.1f, 0.1f, 1f);

            PlayerClass[] choices = GetConfiguredChoices();
            while (optionViews.Count < choices.Length)
                optionViews.Add(CreateOptionView(optionViews.Count));

            for (int i = 0; i < optionViews.Count; i++)
            {
                bool active = i < choices.Length && choices[i] != null;
                optionViews[i].GameObject.SetActive(active);
                if (!active)
                    continue;

                optionViews[i].PlayerClass = choices[i];
                optionViews[i].Text.text = choices[i].className;
                TMP_FontAsset resolvedFont = ResolveOptionFont();
                if (resolvedFont != null)
                    optionViews[i].Text.font = resolvedFont;
                optionViews[i].Text.fontSize = optionFontSize;
                optionViews[i].Text.rectTransform.sizeDelta = optionBoxSize;
                optionViews[i].Text.alignment = TextAlignmentOptions.Center;

                Renderer textRenderer = optionViews[i].Text.GetComponent<Renderer>();
                if (textRenderer != null)
                    textRenderer.sortingOrder = optionSortingOrder;

                optionViews[i].Collider.size = optionBoxSize;
                optionViews[i].Collider.offset = Vector2.zero;
                optionViews[i].Transform.localPosition = new Vector3(0f, -i * optionSpacing, 0f);
            }
        }

        private OptionView CreateOptionView(int index)
        {
            GameObject option = new GameObject($"ClassOption_{index + 1}");
            option.transform.SetParent(optionsRoot, false);

            TextMeshPro text = option.AddComponent<TextMeshPro>();
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;

            BoxCollider2D collider = option.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            return new OptionView(option, text, collider);
        }

        private bool TryHandleOptionClick()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            Camera cameraToUse = cachedCamera != null ? cachedCamera : Camera.main;
            if (cameraToUse == null)
                return false;

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector2 worldPosition = cameraToUse.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));

            foreach (OptionView option in optionViews)
            {
                if (!option.GameObject.activeSelf || option.PlayerClass == null)
                    continue;
                if (!option.Collider.OverlapPoint(worldPosition))
                    continue;

                SelectClass(option.PlayerClass);
                if (playerMovement == null)
                    playerMovement = FindAnyObjectByType<ClickToMove2D>();
                playerMovement?.SuppressNextClick();
                return true;
            }

            return false;
        }

        private void SelectClass(PlayerClass selectedClass)
        {
            HideOptions();

            bool changed = PlayerManager.Instance != null &&
                PlayerManager.Instance.SetActiveCharacterClass(selectedClass, saveImmediately);
            Say(changed
                ? $"Class changed to {selectedClass.className}."
                : $"You are already a {selectedClass.className}.");
        }

        private TMP_FontAsset ResolveOptionFont() =>
            optionFont != null ? optionFont : dialogueText != null ? dialogueText.font : null;

        private PlayerClass[] GetConfiguredChoices()
        {
            if (classChoices != null && classChoices.Length > 0)
                return classChoices;

            return Array.Empty<PlayerClass>();
        }

        private bool HasAlreadyPickedClass(CharacterData character)
        {
            if (character.playerClass == null)
                return false;
            if (startingClass != null)
                return character.playerClass != startingClass;

            return !string.Equals(character.ClassName, "Normie", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class OptionView
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly TextMeshPro Text;
            public readonly BoxCollider2D Collider;
            public PlayerClass PlayerClass;

            public OptionView(GameObject gameObject, TextMeshPro text, BoxCollider2D collider)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                Text = text;
                Collider = collider;
            }
        }
    }
}
