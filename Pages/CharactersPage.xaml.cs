// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SaveOver.Sheltered2.Models;
using SaveOver.Sheltered2.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Lets the user pick a character and edit their basics, health, conditions and stats.
/// </summary>
public sealed partial class CharactersPage : Page
{
    private readonly ObservableCollection<Character> characters = [];

    // Guards the field -> model write-back handlers while we are pushing model values
    // into the controls, so populating the UI doesn't look like a user edit.
    private bool isPopulating;

    // The stat tree currently shown in the Skills section (driven by the SelectorBar).
    private string currentSkillStat = SkillCatalog.Stats[0];

    // The character list currently bound to the combo box, so we can tell an unchanged
    // revisit (keep the selection) from newly loaded data (rebuild).
    private IReadOnlyList<Character>? boundCharacters;

    // The relationship rows currently shown, so the min/max buttons can drive them.
    private IReadOnlyList<RelationshipRowViewModel> relationshipRows = [];

    public CharactersPage()
    {
        InitializeComponent();

        // The page is cached, so wire handlers once here rather than in Loaded.
        CharacterComboBox.ItemsSource = characters;
        WireEditingHandlers();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        App.CurrentSaveData.SaveDataChanged += OnSaveDataChanged;
        PopulateCharacterComboBox();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.CurrentSaveData.SaveDataChanged -= OnSaveDataChanged;
        base.OnNavigatedFrom(e);
    }

    private void OnSaveDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(PopulateCharacterComboBox);

    /// <summary>
    /// Fills the combo box from the shared save data, or disables editing if none exists.
    /// </summary>
    private void PopulateCharacterComboBox()
    {
        IReadOnlyList<Character> source = App.CurrentSaveData.Characters;

        // Same data as last time means a cached-page revisit; keep the selection.
        if (ReferenceEquals(source, boundCharacters))
        {
            return;
        }

        characters.Clear();
        boundCharacters = null;

        if (!App.CurrentSaveData.IsLoaded || source.Count == 0)
        {
            DisableCharacterEditing("No characters found");
            return;
        }

        foreach (Character character in source)
        {
            characters.Add(character);
        }

        boundCharacters = source;
        CharacterComboBox.IsEnabled = true;

        // Restore the last selected character if still present, else the first.
        int rememberedIndex = App.CurrentSaveData.SelectedCharacter is Character remembered
            ? characters.IndexOf(remembered)
            : -1;
        CharacterComboBox.SelectedIndex = rememberedIndex >= 0 ? rememberedIndex : 0;
    }

    /// <summary>Handles selection changes in the character combo box.</summary>
    private void CharacterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character selectedCharacter)
        {
            App.CurrentSaveData.SelectedCharacter = selectedCharacter;
            UpdateCharacterUi(selectedCharacter);
        }
        else
        {
            DisableAllFields();
            SkillTiersItemsControl.ItemsSource = null;
        }
    }

    /// <summary>Pushes a character's values into the editor controls.</summary>
    private void UpdateCharacterUi(Character character)
    {
        isPopulating = true;
        try
        {
            EnableAllFields();

            FirstNameTextBox.Text = character.FirstName;
            LastNameTextBox.Text = character.LastName;

            CurrentHealthNumberBox.Value = character.CurrentHealth;
            MaxHealthNumberBox.Value = character.MaxHealth;

            InteractingCheckBox.IsChecked = character.Interacting;
            InteractingWithObjCheckBox.IsChecked = character.InteractingWithObj;
            IsPsychoCheckBox.IsChecked = character.IsPsycho;
            HasBeenDefibbedCheckBox.IsChecked = character.HasBeenDefibbed;
            PassedOutCheckBox.IsChecked = character.PassedOut;
            IsUnconsciousCheckBox.IsChecked = character.IsUnconscious;
            ResetPositionCheckBox.IsChecked = character.ResetPositionRequested;

            // Needs display as whole numbers; an untouched need keeps its exact saved value.
            HungerNumberBox.Value = RoundNeed(character.Hunger);
            ThirstNumberBox.Value = RoundNeed(character.Thirst);
            FatigueNumberBox.Value = RoundNeed(character.Fatigue);
            DirtinessNumberBox.Value = RoundNeed(character.Dirtiness);
            ToiletNumberBox.Value = RoundNeed(character.Toilet);
            StressNumberBox.Value = RoundNeed(character.Stress);

            // Per-action feedback shouldn't linger across a character switch.
            StatsFeedbackTextBlock.Text = string.Empty;
            SkillsFeedbackTextBlock.Text = string.Empty;
            NeedsFeedbackTextBlock.Text = string.Empty;
            RelationshipsFeedbackTextBlock.Text = string.Empty;

            PopulateStatsData(character);
            PopulateRelationships(character);
            RefreshSkillTree();
        }
        finally
        {
            isPopulating = false;
        }
    }

    /// <summary>Rebuilds the relationship rows, resolving member ids to display names.</summary>
    private void PopulateRelationships(Character character)
    {
        List<RelationshipRowViewModel> rows = [.. character.Relationships
            .Select(relationship => new RelationshipRowViewModel(
                relationship,
                App.CurrentSaveData.Characters
                    .FirstOrDefault(other => other.UniqueId == relationship.MemberId)?.FullName
                    ?? $"Member {relationship.MemberId}"))];

        relationshipRows = rows;
        RelationshipsItemsControl.ItemsSource = rows;
        NoRelationshipsTextBlock.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Pushes a character's stat levels and derived caps into the stat grid.</summary>
    private void PopulateStatsData(Character character)
    {
        StrengthLevelBox.Value = character.Strength.Level;
        DexterityLevelBox.Value = character.Dexterity.Level;
        IntelligenceLevelBox.Value = character.Intelligence.Level;
        CharismaLevelBox.Value = character.Charisma.Level;
        PerceptionLevelBox.Value = character.Perception.Level;
        FortitudeLevelBox.Value = character.Fortitude.Level;

        StrengthCapBox.Value = character.Strength.Cap;
        DexterityCapBox.Value = character.Dexterity.Cap;
        IntelligenceCapBox.Value = character.Intelligence.Cap;
        CharismaCapBox.Value = character.Charisma.Cap;
        PerceptionCapBox.Value = character.Perception.Cap;
        FortitudeCapBox.Value = character.Fortitude.Cap;
    }

    /// <summary>Sets every stat to its maximum level and refreshes the grid.</summary>
    private void MaxStatsButton_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is not Character selectedCharacter)
        {
            return;
        }

        selectedCharacter.Strength.Level = Stat.MaxLevel;
        selectedCharacter.Dexterity.Level = Stat.MaxLevel;
        selectedCharacter.Intelligence.Level = Stat.MaxLevel;
        selectedCharacter.Charisma.Level = Stat.MaxLevel;
        selectedCharacter.Perception.Level = Stat.MaxLevel;
        selectedCharacter.Fortitude.Level = Stat.MaxLevel;

        isPopulating = true;
        try
        {
            PopulateStatsData(selectedCharacter);
        }
        finally
        {
            isPopulating = false;
        }

        StatsFeedbackTextBlock.Text = $"All stats maximised to level {Stat.MaxLevel}.";
    }

    /// <summary>Sets every stat to its minimum level and refreshes the grid.</summary>
    private void MinStatsButton_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is not Character selectedCharacter)
        {
            return;
        }

        selectedCharacter.Strength.Level = Stat.MinLevel;
        selectedCharacter.Dexterity.Level = Stat.MinLevel;
        selectedCharacter.Intelligence.Level = Stat.MinLevel;
        selectedCharacter.Charisma.Level = Stat.MinLevel;
        selectedCharacter.Perception.Level = Stat.MinLevel;
        selectedCharacter.Fortitude.Level = Stat.MinLevel;

        isPopulating = true;
        try
        {
            PopulateStatsData(selectedCharacter);
        }
        finally
        {
            isPopulating = false;
        }

        StatsFeedbackTextBlock.Text = $"All stats minimised to level {Stat.MinLevel}.";
    }

    /// <summary>Sets every relationship to its maximum (100).</summary>
    private void MaxRelationshipsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllRelationships(RelationshipRowViewModel.MaxLevel, "All relationships set to 100.");

    /// <summary>Sets every relationship to its minimum (-100).</summary>
    private void MinRelationshipsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllRelationships(RelationshipRowViewModel.MinLevel, "All relationships set to -100.");

    // Writing through the row view models updates both the model and the bound boxes.
    private void SetAllRelationships(int level, string feedback)
    {
        if (relationshipRows.Count == 0)
        {
            return;
        }

        foreach (RelationshipRowViewModel row in relationshipRows)
        {
            row.Level = level;
        }

        RelationshipsFeedbackTextBlock.Text = feedback;
    }

    /// <summary>Sets every need to 0 (fully satisfied).</summary>
    private void SatisfyNeedsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllNeeds(0, "All needs satisfied.");

    /// <summary>Sets every need to 100 (critical).</summary>
    private void DepleteNeedsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllNeeds(100, "All needs set to critical.");

    private void SetAllNeeds(double value, string feedback)
    {
        if (CharacterComboBox.SelectedItem is not Character character)
        {
            return;
        }

        character.Hunger = value;
        character.Thirst = value;
        character.Fatigue = value;
        character.Dirtiness = value;
        character.Toilet = value;
        character.Stress = value;

        isPopulating = true;
        try
        {
            HungerNumberBox.Value = value;
            ThirstNumberBox.Value = value;
            FatigueNumberBox.Value = value;
            DirtinessNumberBox.Value = value;
            ToiletNumberBox.Value = value;
            StressNumberBox.Value = value;
        }
        finally
        {
            isPopulating = false;
        }

        NeedsFeedbackTextBlock.Text = feedback;
    }

    #region Skills

    /// <summary>Rebuilds the skill tree shown for the current stat and selected character.</summary>
    private void RefreshSkillTree()
    {
        if (CharacterComboBox.SelectedItem is not Character character)
        {
            SkillTiersItemsControl.ItemsSource = null;
            return;
        }

        SkillTiersItemsControl.ItemsSource = BuildSkillTree(character, currentSkillStat);
    }

    /// <summary>
    /// Builds the tier view models for a stat, seeded from the character's saved levels.
    /// </summary>
    private List<SkillTierViewModel> BuildSkillTree(Character character, string stat)
    {
        Dictionary<int, int> levelsByKey = [];
        ObservableCollection<SkillInstance>? tree = character.GetSkillTree(stat);
        if (tree is not null)
        {
            foreach (SkillInstance skill in tree)
            {
                levelsByKey[skill.Key] = skill.Level;
            }
        }

        // Ignore late changes from recycled template items after a character switch.
        void WriteBack(SkillDefinition definition, int level)
        {
            if (ReferenceEquals(CharacterComboBox.SelectedItem, character))
            {
                ApplySkillLevel(character, definition, level);
            }
        }

        return [.. SkillCatalog.ForStat(stat)
            .GroupBy(definition => definition.Tier)
            .OrderBy(group => group.Key)
            .Select(group => new SkillTierViewModel(
                group.Key,
                [.. group.Select(definition =>
                    new SkillSlotViewModel(definition, levelsByKey.GetValueOrDefault(definition.Key), WriteBack))]))];
    }

    // The save only lists unlocked skills, so level 0 removes the entry.
    private static void ApplySkillLevel(Character character, SkillDefinition definition, int level)
    {
        ObservableCollection<SkillInstance>? tree = character.GetSkillTree(definition.Stat);
        if (tree is null)
        {
            return;
        }

        int clampedLevel = Math.Clamp(level, 0, definition.MaxLevel);
        SkillInstance? existing = tree.FirstOrDefault(skill => skill.Key == definition.Key);

        if (clampedLevel == 0)
        {
            if (existing is not null)
            {
                tree.Remove(existing);
            }
        }
        else if (existing is not null)
        {
            existing.Level = clampedLevel;
        }
        else
        {
            tree.Add(new SkillInstance(definition.Key, clampedLevel));
        }
    }

    /// <summary>Switches the visible skill tree when the stat selector changes.</summary>
    private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string stat)
        {
            currentSkillStat = stat;
            SkillsFeedbackTextBlock.Text = string.Empty;
            RefreshSkillTree();
        }
    }

    /// <summary>Maximises every skill in the currently viewed tree.</summary>
    private void MaxTreeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character character)
        {
            SetSkillLevels(character, SkillCatalog.ForStat(currentSkillStat), maximise: true);
            SkillsFeedbackTextBlock.Text = $"All {currentSkillStat} skills maximised.";
        }
    }

    /// <summary>Maximises every skill in every tree.</summary>
    private void MaxAllTreesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character character)
        {
            SetSkillLevels(character, SkillCatalog.All, maximise: true);
            SkillsFeedbackTextBlock.Text = "All skill trees maximised.";
        }
    }

    /// <summary>Removes every invested point in the currently viewed tree.</summary>
    private void ClearTreeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character character)
        {
            SetSkillLevels(character, SkillCatalog.ForStat(currentSkillStat), maximise: false);
            SkillsFeedbackTextBlock.Text = $"All {currentSkillStat} skill points removed.";
        }
    }

    /// <summary>Removes every invested point in every tree.</summary>
    private void ClearAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character character)
        {
            SetSkillLevels(character, SkillCatalog.All, maximise: false);
            SkillsFeedbackTextBlock.Text = "All skill points removed.";
        }
    }

    private void SetSkillLevels(Character character, IEnumerable<SkillDefinition> definitions, bool maximise)
    {
        foreach (SkillDefinition definition in definitions)
        {
            ApplySkillLevel(character, definition, maximise ? definition.MaxLevel : 0);
        }

        RefreshSkillTree();
    }

    #endregion

    /// <summary>Attaches the control -> model write-back handlers exactly once.</summary>
    private void WireEditingHandlers()
    {
        FirstNameTextBox.TextChanged += (s, e) =>
        {
            if (!isPopulating && CharacterComboBox.SelectedItem is Character character)
            {
                character.FirstName = FirstNameTextBox.Text;
            }
        };

        LastNameTextBox.TextChanged += (s, e) =>
        {
            if (!isPopulating && CharacterComboBox.SelectedItem is Character character)
            {
                character.LastName = LastNameTextBox.Text;
            }
        };

        CurrentHealthNumberBox.ValueChanged += (s, e) =>
        {
            if (!isPopulating && CharacterComboBox.SelectedItem is Character character && !double.IsNaN(e.NewValue))
            {
                character.CurrentHealth = (int)e.NewValue;
            }
        };

        MaxHealthNumberBox.ValueChanged += (s, e) =>
        {
            if (!isPopulating && CharacterComboBox.SelectedItem is Character character && !double.IsNaN(e.NewValue))
            {
                character.MaxHealth = (int)e.NewValue;
            }
        };

        WireStatLevel(StrengthLevelBox, StrengthCapBox, c => c.Strength);
        WireStatLevel(DexterityLevelBox, DexterityCapBox, c => c.Dexterity);
        WireStatLevel(IntelligenceLevelBox, IntelligenceCapBox, c => c.Intelligence);
        WireStatLevel(CharismaLevelBox, CharismaCapBox, c => c.Charisma);
        WireStatLevel(PerceptionLevelBox, PerceptionCapBox, c => c.Perception);
        WireStatLevel(FortitudeLevelBox, FortitudeCapBox, c => c.Fortitude);

        WireCheckBox(InteractingCheckBox, (c, v) => c.Interacting = v);
        WireCheckBox(InteractingWithObjCheckBox, (c, v) => c.InteractingWithObj = v);
        WireCheckBox(IsPsychoCheckBox, (c, v) => c.IsPsycho = v);
        WireCheckBox(HasBeenDefibbedCheckBox, (c, v) => c.HasBeenDefibbed = v);
        WireCheckBox(PassedOutCheckBox, (c, v) => c.PassedOut = v);
        WireCheckBox(IsUnconsciousCheckBox, (c, v) => c.IsUnconscious = v);
        WireCheckBox(ResetPositionCheckBox, (c, v) => c.ResetPositionRequested = v);

        WireNeed(HungerNumberBox, (c, v) => c.Hunger = v);
        WireNeed(ThirstNumberBox, (c, v) => c.Thirst = v);
        WireNeed(FatigueNumberBox, (c, v) => c.Fatigue = v);
        WireNeed(DirtinessNumberBox, (c, v) => c.Dirtiness = v);
        WireNeed(ToiletNumberBox, (c, v) => c.Toilet = v);
        WireNeed(StressNumberBox, (c, v) => c.Stress = v);
    }

    private void WireNeed(NumberBox box, Action<Character, double> apply)
    {
        box.ValueChanged += (s, e) =>
        {
            if (isPopulating || CharacterComboBox.SelectedItem is not Character character || double.IsNaN(e.NewValue))
            {
                return;
            }

            // Edits commit whole numbers; snap the box in case the user typed a fraction.
            double whole = RoundNeed(Math.Clamp(e.NewValue, 0, 100));
            apply(character, whole);
            if (box.Value != whole)
            {
                box.Value = whole;
            }
        };
    }

    /// <summary>Rounds a raw 0-100 need to the nearest whole number for display/editing.</summary>
    private static double RoundNeed(double value) =>
        Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero);

    private void WireStatLevel(NumberBox levelBox, NumberBox capBox, Func<Character, Stat> selectStat)
    {
        levelBox.ValueChanged += (s, e) =>
        {
            if (isPopulating || CharacterComboBox.SelectedItem is not Character character)
            {
                return;
            }

            Stat stat = selectStat(character);
            stat.Level = double.IsNaN(e.NewValue) ? Stat.MinLevel : (int)e.NewValue;
            capBox.Value = stat.Cap; // Keep the derived cap in sync as the level changes.
        };
    }

    private void WireCheckBox(CheckBox checkBox, Action<Character, bool> apply)
    {
        void Handler(object sender, RoutedEventArgs e)
        {
            if (!isPopulating && CharacterComboBox.SelectedItem is Character character)
            {
                apply(character, checkBox.IsChecked ?? false);
            }
        }

        checkBox.Checked += Handler;
        checkBox.Unchecked += Handler;
    }

    private void DisableCharacterEditing(string message)
    {
        CharacterComboBox.IsEnabled = false;
        CharacterComboBox.PlaceholderText = message;
        DisableAllFields();
        SkillTiersItemsControl.ItemsSource = null;
        RelationshipsItemsControl.ItemsSource = null;
        relationshipRows = [];
    }

    private void EnableAllFields() => SetFieldsEnabled(true);

    private void DisableAllFields() => SetFieldsEnabled(false);

    private void SetFieldsEnabled(bool enabled)
    {
        FirstNameTextBox.IsEnabled = enabled;
        LastNameTextBox.IsEnabled = enabled;
        CurrentHealthNumberBox.IsEnabled = enabled;
        MaxHealthNumberBox.IsEnabled = enabled;
        MaxStatsButton.IsEnabled = enabled;
        MinStatsButton.IsEnabled = enabled;

        InteractingCheckBox.IsEnabled = enabled;
        InteractingWithObjCheckBox.IsEnabled = enabled;
        IsPsychoCheckBox.IsEnabled = enabled;
        HasBeenDefibbedCheckBox.IsEnabled = enabled;
        PassedOutCheckBox.IsEnabled = enabled;
        IsUnconsciousCheckBox.IsEnabled = enabled;
        ResetPositionCheckBox.IsEnabled = enabled;

        HungerNumberBox.IsEnabled = enabled;
        ThirstNumberBox.IsEnabled = enabled;
        FatigueNumberBox.IsEnabled = enabled;
        DirtinessNumberBox.IsEnabled = enabled;
        ToiletNumberBox.IsEnabled = enabled;
        StressNumberBox.IsEnabled = enabled;
        SatisfyNeedsButton.IsEnabled = enabled;
        DepleteNeedsButton.IsEnabled = enabled;
        RelationshipsItemsControl.IsEnabled = enabled;
        MaxRelationshipsButton.IsEnabled = enabled;
        MinRelationshipsButton.IsEnabled = enabled;

        StrengthLevelBox.IsEnabled = enabled;
        DexterityLevelBox.IsEnabled = enabled;
        IntelligenceLevelBox.IsEnabled = enabled;
        CharismaLevelBox.IsEnabled = enabled;
        PerceptionLevelBox.IsEnabled = enabled;
        FortitudeLevelBox.IsEnabled = enabled;

        SelectorBar.IsEnabled = enabled;
        SkillActionsButton.IsEnabled = enabled;
        SkillTiersItemsControl.IsEnabled = enabled;
    }
}
