// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SaveOver.Sheltered2.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// Lets the user pick a pet and edit its basics, condition and training skills.
/// </summary>
public sealed partial class PetsPage : Page
{
    private readonly ObservableCollection<Pet> pets = [];

    // True while model values are being pushed into the controls, so populating the UI
    // doesn't look like a user edit.
    private bool isPopulating;

    // Bound list marker: an unchanged revisit keeps the selection, new data rebuilds it.
    private IReadOnlyList<Pet>? boundPets;

    public PetsPage()
    {
        InitializeComponent();
        PetComboBox.ItemsSource = pets;
        WireEditingHandlers();
    }

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

    private void PopulatePetComboBox()
    {
        IReadOnlyList<Pet> source = App.CurrentSaveData.Pets;

        if (ReferenceEquals(source, boundPets))
        {
            return;
        }

        pets.Clear();
        boundPets = null;

        if (!App.CurrentSaveData.IsLoaded || source.Count == 0)
        {
            PetComboBox.IsEnabled = false;
            PetComboBox.PlaceholderText = "No pets found";
            SetFieldsEnabled(false);
            return;
        }

        foreach (Pet pet in source)
        {
            pets.Add(pet);
        }

        boundPets = source;
        PetComboBox.IsEnabled = true;
        PetComboBox.SelectedIndex = 0;
    }

    private void PetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PetComboBox.SelectedItem is Pet pet)
        {
            UpdatePetUi(pet);
        }
        else
        {
            SetFieldsEnabled(false);
        }
    }

    private void UpdatePetUi(Pet pet)
    {
        isPopulating = true;
        try
        {
            SetFieldsEnabled(true);

            PetNameTextBox.Text = pet.Name;
            PetAgeNumberBox.Value = pet.Age;
            PetHealthNumberBox.Value = pet.Health;

            // Hunger displays as a whole number; untouched it keeps its exact saved value.
            PetHungerNumberBox.Value = RoundHunger(pet.Hunger);

            TrainingFeedbackTextBlock.Text = string.Empty;

            PetStarvingCheckBox.IsChecked = pet.Starving;
            PetPoisonedCheckBox.IsChecked = pet.Poisoned;
            PetImmuneCheckBox.IsChecked = pet.Immune;

            PreyDriveLevelBox.Value = pet.PreyDrive.Level;
            PreyDriveCapBox.Value = pet.PreyDrive.LevelCap;
            PreyDriveXpBox.Value = pet.PreyDrive.Experience;

            ScavengingLevelBox.Value = pet.Scavenging.Level;
            ScavengingCapBox.Value = pet.Scavenging.LevelCap;
            ScavengingXpBox.Value = pet.Scavenging.Experience;

            AffectionLevelBox.Value = pet.Affection.Level;
            AffectionCapBox.Value = pet.Affection.LevelCap;
            AffectionXpBox.Value = pet.Affection.Experience;
        }
        finally
        {
            isPopulating = false;
        }
    }

    /// <summary>Attaches the control -> model write-back handlers exactly once.</summary>
    private void WireEditingHandlers()
    {
        PetNameTextBox.TextChanged += (s, e) =>
        {
            if (!isPopulating && PetComboBox.SelectedItem is Pet pet)
            {
                pet.Name = PetNameTextBox.Text;
            }
        };
        PetNameTextBox.LostFocus += (_, _) =>
            PetNameTextBox.Text = PetNameTextBox.Text.Trim();

        WireIntegerNumber(PetAgeNumberBox, (p, v) => p.Age = v);
        WireIntegerNumber(PetHealthNumberBox, (p, v) => p.Health = v);
        WireHunger();

        WireCheckBox(PetStarvingCheckBox, (p, v) => p.Starving = v);
        WireCheckBox(PetPoisonedCheckBox, (p, v) => p.Poisoned = v);
        WireCheckBox(PetImmuneCheckBox, (p, v) => p.Immune = v);

        WireIntegerNumber(PreyDriveLevelBox, (p, v) => p.PreyDrive.Level = v);
        WireIntegerNumber(PreyDriveCapBox, (p, v) => p.PreyDrive.LevelCap = v);
        WireIntegerNumber(PreyDriveXpBox, (p, v) => p.PreyDrive.Experience = v);

        WireIntegerNumber(ScavengingLevelBox, (p, v) => p.Scavenging.Level = v);
        WireIntegerNumber(ScavengingCapBox, (p, v) => p.Scavenging.LevelCap = v);
        WireIntegerNumber(ScavengingXpBox, (p, v) => p.Scavenging.Experience = v);

        WireIntegerNumber(AffectionLevelBox, (p, v) => p.Affection.Level = v);
        WireIntegerNumber(AffectionCapBox, (p, v) => p.Affection.LevelCap = v);
        WireIntegerNumber(AffectionXpBox, (p, v) => p.Affection.Experience = v);
    }

    private void WireIntegerNumber(NumberBox box, Action<Pet, int> apply) => box.ValueChanged += (s, e) =>
    {
        if (isPopulating || PetComboBox.SelectedItem is not Pet pet || double.IsNaN(e.NewValue))
        {
            return;
        }

        int value = (int)e.NewValue;
        apply(pet, value);
        if (box.Value != value)
        {
            box.Value = value;
        }
    };

    // Edits commit whole numbers; snap the box in case the user typed a fraction.
    private void WireHunger() => PetHungerNumberBox.ValueChanged += (s, e) =>
    {
        if (isPopulating || PetComboBox.SelectedItem is not Pet pet || double.IsNaN(e.NewValue))
        {
            return;
        }

        double whole = RoundHunger(e.NewValue);
        pet.Hunger = whole;
        if (PetHungerNumberBox.Value != whole)
        {
            PetHungerNumberBox.Value = whole;
        }
    };

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

        isPopulating = true;
        try
        {
            PreyDriveLevelBox.Value = pet.PreyDrive.Level;
            PreyDriveXpBox.Value = pet.PreyDrive.Experience;
            ScavengingLevelBox.Value = pet.Scavenging.Level;
            ScavengingXpBox.Value = pet.Scavenging.Experience;
            AffectionLevelBox.Value = pet.Affection.Level;
            AffectionXpBox.Value = pet.Affection.Experience;
        }
        finally
        {
            isPopulating = false;
        }

        TrainingFeedbackTextBlock.Text = feedback;
    }

    private static double RoundHunger(double value) =>
        Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero);

    private void WireCheckBox(CheckBox checkBox, Action<Pet, bool> apply)
    {
        void Handler(object sender, RoutedEventArgs e)
        {
            if (!isPopulating && PetComboBox.SelectedItem is Pet pet)
            {
                apply(pet, checkBox.IsChecked ?? false);
            }
        }

        checkBox.Checked += Handler;
        checkBox.Unchecked += Handler;
    }

    private void SetFieldsEnabled(bool enabled)
    {
        PetNameTextBox.IsEnabled = enabled;
        PetAgeNumberBox.IsEnabled = enabled;
        PetHealthNumberBox.IsEnabled = enabled;
        PetHungerNumberBox.IsEnabled = enabled;

        PetStarvingCheckBox.IsEnabled = enabled;
        PetPoisonedCheckBox.IsEnabled = enabled;
        PetImmuneCheckBox.IsEnabled = enabled;

        MaxTrainingButton.IsEnabled = enabled;
        MinTrainingButton.IsEnabled = enabled;

        PreyDriveLevelBox.IsEnabled = enabled;
        PreyDriveCapBox.IsEnabled = enabled;
        PreyDriveXpBox.IsEnabled = enabled;
        ScavengingLevelBox.IsEnabled = enabled;
        ScavengingCapBox.IsEnabled = enabled;
        ScavengingXpBox.IsEnabled = enabled;
        AffectionLevelBox.IsEnabled = enabled;
        AffectionCapBox.IsEnabled = enabled;
        AffectionXpBox.IsEnabled = enabled;
    }
}
