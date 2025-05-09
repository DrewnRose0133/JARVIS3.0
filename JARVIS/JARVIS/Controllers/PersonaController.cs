using System;

namespace JARVIS.Controllers
{
    // Enums combined for unified persona control
    public enum Mood
    {
        Serious,
        Lighthearted,
        Emergency
    }

    public enum CharacterMode
    {
        Advisor,
        Companion,
        Emergency,
        Silent
    }

    /// <summary>
    /// Unified controller combining mood and character mode behaviors.
    /// </summary>
    public class PersonaController
    {
        // Current state
        public Mood CurrentMood { get; private set; } = Mood.Lighthearted;
        public CharacterMode CurrentMode { get; private set; } = CharacterMode.Advisor;
        public bool SarcasmEnabled { get; private set; } = true;

        // Events
        public event Action<Mood> MoodChanged;
        public event Action<CharacterMode> ModeChanged;

        // === Mood methods ===
        public void AdjustMoodBasedOnWeather(string weatherDescription)
        {
            if (string.IsNullOrWhiteSpace(weatherDescription)) return;
            weatherDescription = weatherDescription.ToLowerInvariant();

            var newMood = CurrentMood;
            if (weatherDescription.Contains("storm") || weatherDescription.Contains("alert") || weatherDescription.Contains("tornado"))
                newMood = Mood.Emergency;
            else if (weatherDescription.Contains("rain") || weatherDescription.Contains("cloud"))
                newMood = Mood.Serious;
            else if (weatherDescription.Contains("sunny") || weatherDescription.Contains("clear"))
                newMood = Mood.Lighthearted;

            SetMood(newMood);
        }

        public void AdjustToneBasedOnAttitude(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput)) return;
            userInput = userInput.ToLowerInvariant();
            SarcasmEnabled = !(userInput.Contains("please") || userInput.Contains("thank you"));
        }

        public void SetMood(Mood newMood)
        {
            if (CurrentMood == newMood) return;
            CurrentMood = newMood;
            MoodChanged?.Invoke(newMood);
        }

        public string DescribeMood()
            => CurrentMood switch
            {
                Mood.Serious => "I’m in a serious and focused state.",
                Mood.Lighthearted => "I’m feeling light and cheerful.",
                Mood.Emergency => "Emergency protocols activated!",
                _ => "Just your average helpful assistant."
            };

        public void ApplyMoodPreset(string preset)
        {
            switch (preset?.ToLowerInvariant())
            {
                case "witty advisor":
                    SetMood(Mood.Lighthearted);
                    SarcasmEnabled = true;
                    break;
                case "formal assistant":
                    SetMood(Mood.Serious);
                    SarcasmEnabled = false;
                    break;
                case "emergency mode":
                    SetMood(Mood.Emergency);
                    SarcasmEnabled = false;
                    break;
                default:
                    SetMood(Mood.Serious);
                    SarcasmEnabled = false;
                    break;
            }
        }

        // === Character mode methods ===
        public void SetMode(CharacterMode newMode)
        {
            if (CurrentMode == newMode) return;
            CurrentMode = newMode;
            ModeChanged?.Invoke(newMode);
        }

        public void ApplyModePreset(string preset)
        {
            switch (preset?.ToLowerInvariant())
            {
                case "advisor":
                case "witty advisor":
                    SetMode(CharacterMode.Advisor);
                    break;
                case "companion":
                    SetMode(CharacterMode.Companion);
                    break;
                case "emergency":
                case "emergency mode":
                    SetMode(CharacterMode.Emergency);
                    break;
                case "silent":
                    SetMode(CharacterMode.Silent);
                    break;
                default:
                    SetMode(CharacterMode.Advisor);
                    break;
            }
        }

        // === Combined Personality method ===
        /// <summary>
        /// Applies both mood and character-mode presets based on a single personality name.
        /// </summary>
        public void ApplyPersonalityPreset(string preset)
        {
            ApplyModePreset(preset);
            ApplyMoodPreset(preset);
        }

        public string GetSignatureResponse()
            => CurrentMode switch
            {
                CharacterMode.Advisor => "Very good, sir.",
                CharacterMode.Companion => "You got it. Always happy to help.",
                CharacterMode.Emergency => "Directive acknowledged. Executing now.",
                CharacterMode.Silent => string.Empty,
                _ => "At your service."
            };

        public string GetPreamble()
            => CurrentMode switch
            {
                CharacterMode.Advisor => "As you requested, sir:",
                CharacterMode.Companion => "Sure thing, here's what I found:",
                CharacterMode.Emergency => "Priority instruction received:",
                CharacterMode.Silent => string.Empty,
                _ => string.Empty
            };

        public string DescribeMode()
            => CurrentMode switch
            {
                CharacterMode.Advisor => "Strategic, formal, and precise.",
                CharacterMode.Companion => "Friendly, humorous, and casual.",
                CharacterMode.Emergency => "Tactical, fast, and serious.",
                CharacterMode.Silent => "Silent mode engaged. No speech output.",
                _ => "Neutral operational mode."
            };

        // === Combined Persona Description ===
        public string DescribePersona()
            => $"Mode: {DescribeMode()} | Mood: {DescribeMood()}";
    }
}
