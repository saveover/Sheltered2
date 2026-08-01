// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Keeps parsed and session-created pets on the same editing surface. Species-specific panels are
/// presentation only; model identity and XML materialization remain in SaveSession/SaveWriter.
/// </summary>
public sealed partial class PetsPage : Page
{
    // Bound list marker: an unchanged revisit keeps the selection, new data rebuilds it.
    private IReadOnlyList<Pet>? boundPets;

    public PetsPage() => InitializeComponent();

    /// <summary>Public because compiled page bindings need to switch roots when selection changes.</summary>
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
        AddPetButton.IsEnabled = App.CurrentSaveData.IsLoaded && App.CurrentSaveData.CanAddPets;

        if (ReferenceEquals(source, boundPets))
        {
            return;
        }

        PetComboBox.ItemsSource = null;
        boundPets = null;

        if (!App.CurrentSaveData.IsLoaded || source.Count == 0)
        {
            SelectedPet = null;
            App.CurrentSaveData.SelectedPet = null;
            Bindings.Update();
            PetComboBox.IsEnabled = false;
            PetComboBox.PlaceholderText = "No pets found";
            SetFieldsEnabled(false);
            UpdateTrainingPanels();
            return;
        }

        PetComboBox.ItemsSource = source;
        boundPets = source;
        PetComboBox.IsEnabled = true;
        PetComboBox.PlaceholderText = "Select a pet";
        PetComboBox.SelectedItem = App.CurrentSaveData.SelectedPet is { } selected
            && source.Contains(selected)
                ? selected
                : source[0];
    }

    private void PetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PetComboBox.SelectedItem is Pet pet)
        {
            SelectedPet = pet;
            App.CurrentSaveData.SelectedPet = pet;
            Bindings.Update();
            SetFieldsEnabled(true);
            UpdateTrainingPanels();
            TrainingFeedbackTextBlock.Text = string.Empty;
        }
        else
        {
            SelectedPet = null;
            Bindings.Update();
            SetFieldsEnabled(false);
            UpdateTrainingPanels();
        }
    }

    private void AddDogMenuItem_Click(object sender, RoutedEventArgs e) => AddPet(PetSpecies.Dog);

    private void AddCatMenuItem_Click(object sender, RoutedEventArgs e) => AddPet(PetSpecies.Cat);

    private void AddPet(PetSpecies species)
    {
        // Rebind rather than appending to the ComboBox directly: SaveSession replaces its immutable
        // list so every page receives the same collection identity and selected instance.
        Pet pet = App.CurrentSaveData.AddPet(species);
        boundPets = null;
        PopulatePetComboBox();
        PetComboBox.SelectedItem = pet;
        TrainingFeedbackTextBlock.Text = $"Added a new {pet.SpeciesName.ToLowerInvariant()}. Save the file to write it into the shelter.";
    }

    private void MaxTrainingButton_Click(object sender, RoutedEventArgs e) =>
        SetAllTraining(skill => skill.LevelCap, "All training skills raised to their level cap.");

    private void MinTrainingButton_Click(object sender, RoutedEventArgs e) =>
        SetAllTraining(_ => 1, "All training skills reset to level 1.");

    private void TrainAllDogSkillsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllDogSkills(2, "All dog skills marked as fully trained.");

    private void RemoveAllDogSkillsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllDogSkills(0, "All dog skills marked as not purchased.");

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

    private void SetAllDogSkills(int stateIndex, string feedback)
    {
        if (PetComboBox.SelectedItem is not Pet { IsDog: true } pet)
        {
            return;
        }

        foreach (DogSkill skill in pet.DogSkills)
        {
            skill.SelectedStateIndex = stateIndex;
        }

        TrainingFeedbackTextBlock.Text = feedback;
    }

    private void UpdateTrainingPanels()
    {
        CatTrainingPanel.Visibility = SelectedPet?.IsCat == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        DogTrainingPanel.Visibility = SelectedPet?.IsDog == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SetFieldsEnabled(bool enabled)
    {
        BasicsCardHost.IsEnabled = enabled;
        TrainingCardHost.IsEnabled = enabled;
    }
}
