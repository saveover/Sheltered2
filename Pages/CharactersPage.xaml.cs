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
/// Binds directly to the shared character models so edits remain live across cached-page
/// navigation. Temporary row and skill view models exist only where WinUI controls need a
/// different numeric surface or grouping shape.
/// </summary>
public sealed partial class CharactersPage : Page
{
    // Keep the selected stat outside regenerated tier view models so bulk actions and refreshes
    // target the same tree after template recycling.
    private CharacterStat currentSkillStat = SkillCatalog.Stats[0];

    // The character list currently bound to the combo box, so we can tell an unchanged
    // revisit (keep the selection) from newly loaded data (rebuild).
    private IReadOnlyList<Character>? boundCharacters;

    // The relationship rows currently shown, so the min/max buttons can drive them.
    private IReadOnlyList<RelationshipRowViewModel> relationshipRows = [];

    public CharactersPage() => InitializeComponent();

    /// <summary>Public because compiled page bindings cannot bind to a private selection field.</summary>
    public Character? SelectedCharacter { get; private set; }

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

    private void TrimTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Text = textBox.Text.Trim();
        }
    }

    /// <summary>
    /// Rebinds only when SaveSession replaces the list; a cached-page revisit must not discard the
    /// user's remembered selection or recreate two-way binding targets.
    /// </summary>
    private void PopulateCharacterComboBox()
    {
        IReadOnlyList<Character> source = App.CurrentSaveData.Characters;

        // Same data as last time means a cached-page revisit; keep the selection.
        if (ReferenceEquals(source, boundCharacters))
        {
            return;
        }

        CharacterComboBox.ItemsSource = null;
        boundCharacters = null;

        if (!App.CurrentSaveData.IsLoaded || source.Count == 0)
        {
            DisableCharacterEditing("No characters found");
            return;
        }

        CharacterComboBox.ItemsSource = source;
        boundCharacters = source;
        CharacterComboBox.IsEnabled = true;

        // Restore the last selected character if still present, else the first.
        Character? remembered = App.CurrentSaveData.SelectedCharacter;
        CharacterComboBox.SelectedItem = remembered is not null && source.Contains(remembered)
            ? remembered
            : source[0];
    }

    private void CharacterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character selectedCharacter)
        {
            SelectedCharacter = selectedCharacter;
            App.CurrentSaveData.SelectedCharacter = selectedCharacter;
            // SelectedCharacter is an ordinary page property; force compiled bindings to switch
            // their root before enabling controls that can write back.
            Bindings.Update();
            EnableAllFields();

            StatsFeedbackTextBlock.Text = string.Empty;
            SkillsFeedbackTextBlock.Text = string.Empty;
            NeedsFeedbackTextBlock.Text = string.Empty;
            RelationshipsFeedbackTextBlock.Text = string.Empty;

            PopulateRelationships(selectedCharacter);
            RefreshSkillTree();
        }
        else
        {
            SelectedCharacter = null;
            Bindings.Update();
            DisableAllFields();
            SkillTiersItemsControl.ItemsSource = null;
        }
    }

    /// <summary>
    /// Resolves names for presentation but keeps each row attached to the ID-backed model entry,
    /// because names are editable and not unique.
    /// </summary>
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

        StatsFeedbackTextBlock.Text = $"All stats maximised to level {Stat.MaxLevel}.";
    }

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

        StatsFeedbackTextBlock.Text = $"All stats minimised to level {Stat.MinLevel}.";
    }

    private void MaxRelationshipsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllRelationships(RelationshipRowViewModel.MaxLevel, "All relationships set to 100.");

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

    private void SatisfyNeedsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllNeeds(0, "All needs satisfied.");

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

        NeedsFeedbackTextBlock.Text = feedback;
    }

    #region Skills

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
    /// Builds transient presentation objects from the sparse unlocked-skill list. Write-back checks
    /// the captured character so a recycled control from the previous selection cannot edit the
    /// newly selected character.
    /// </summary>
    private List<SkillTierViewModel> BuildSkillTree(Character character, CharacterStat stat)
    {
        Dictionary<int, int> levelsByKey = [];
        ObservableCollection<SkillInstance> tree = character.GetSkillTree(stat);
        foreach (SkillInstance skill in tree)
        {
            levelsByKey[skill.Key] = skill.Level;
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
        ObservableCollection<SkillInstance> tree = character.GetSkillTree(definition.Stat);

        int clampedLevel = Math.Clamp(level, 0, definition.MaxLevel);
        SkillInstance? existing = tree.FirstOrDefault(skill => skill.Key == definition.Key);

        if (clampedLevel == 0)
        {
            if (existing is not null)
            {
                _ = tree.Remove(existing);
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

    private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string tag &&
            Enum.TryParse(tag, ignoreCase: false, out CharacterStat stat))
        {
            currentSkillStat = stat;
            SkillsFeedbackTextBlock.Text = string.Empty;
            RefreshSkillTree();
        }
    }

    private void MaxTreeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character character)
        {
            SetSkillLevels(character, SkillCatalog.ForStat(currentSkillStat), maximise: true);
            SkillsFeedbackTextBlock.Text = $"All {currentSkillStat} skills maximised.";
        }
    }

    private void MaxAllTreesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character character)
        {
            SetSkillLevels(character, SkillCatalog.All, maximise: true);
            SkillsFeedbackTextBlock.Text = "All skill trees maximised.";
        }
    }

    private void ClearTreeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is Character character)
        {
            SetSkillLevels(character, SkillCatalog.ForStat(currentSkillStat), maximise: false);
            SkillsFeedbackTextBlock.Text = $"All {currentSkillStat} skill points removed.";
        }
    }

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

    private void DisableCharacterEditing(string message)
    {
        SelectedCharacter = null;
        Bindings.Update();
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
        LeftColumnHost.IsEnabled = enabled;
        RightColumnHost.IsEnabled = enabled;
    }
}
