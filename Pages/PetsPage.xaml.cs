// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Lets the user pick a pet and edit its basics, condition and training skills.
/// </summary>
public sealed partial class PetsPage : Page
{
    // Bound list marker: an unchanged revisit keeps the selection, new data rebuilds it.
    private IReadOnlyList<Pet>? boundPets;

    public PetsPage() => InitializeComponent();

    /// <summary>The pet whose observable properties are bound to the editor fields.</summary>
    public Pet? SelectedPet { get; private set; }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        App.CurrentSaveData.SaveDataChanged += OnSaveDataChanged;
        PopulatePetComboBox();
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.CurrentSaveData.SaveDataChanged -= OnSaveDataChanged;
        base.OnNavigatedFrom(e);
    }

    private void OnSaveDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(PopulatePetComboBox);

    private void TrimTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Text = textBox.Text.Trim();
        }
    }

    private void PopulatePetComboBox()
    {
        IReadOnlyList<Pet> source = App.CurrentSaveData.Pets;

        if (ReferenceEquals(source, boundPets))
        {
            return;
        }

        PetComboBox.ItemsSource = null;
        boundPets = null;

        if (!App.CurrentSaveData.IsLoaded || source.Count == 0)
        {
            SelectedPet = null;
            Bindings.Update();
            PetComboBox.IsEnabled = false;
            PetComboBox.PlaceholderText = "No pets found";
            SetFieldsEnabled(false);
            return;
        }

        PetComboBox.ItemsSource = source;
        boundPets = source;
        PetComboBox.IsEnabled = true;
        PetComboBox.SelectedItem = source[0];
    }

    private void PetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PetComboBox.SelectedItem is Pet pet)
        {
            SelectedPet = pet;
            Bindings.Update();
            SetFieldsEnabled(true);
            TrainingFeedbackTextBlock.Text = string.Empty;
        }
        else
        {
            SelectedPet = null;
            Bindings.Update();
            SetFieldsEnabled(false);
        }
    }

    /// <summary>Sets every training skill's level to its cap and clears its experience.</summary>
    private void MaxTrainingButton_Click(object sender, RoutedEventArgs e) =>
        SetAllTraining(skill => skill.LevelCap, "All training skills raised to their level cap.");

    /// <summary>Sets every training skill back to level 1 and clears its experience.</summary>
    private void MinTrainingButton_Click(object sender, RoutedEventArgs e) =>
        SetAllTraining(_ => 1, "All training skills reset to level 1.");

    // Experience is progress toward the next level, so it resets when a level is set directly.
    private void SetAllTraining(Func<PetSkill, int> levelSelector, string feedback)
    {
        if (PetComboBox.SelectedItem is not Pet pet)
        {
            return;
        }

        PetSkill[] skills = [pet.PreyDrive, pet.Scavenging, pet.Affection];
        foreach (PetSkill skill in skills)
        {
            skill.Level = levelSelector(skill);
            skill.Experience = 0;
        }

        TrainingFeedbackTextBlock.Text = feedback;
    }

    private void SetFieldsEnabled(bool enabled)
    {
        BasicsCardHost.IsEnabled = enabled;
        TrainingCardHost.IsEnabled = enabled;
    }
}
