using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

namespace JARVIS.Core
{
    public class AudioEngine
    {
        public SpeechSynthesizer InitializeSynthesizer()
        {
            var synthesizer = new SpeechSynthesizer();
            synthesizer.SetOutputToDefaultAudioDevice();
            synthesizer.Volume = 100;
            synthesizer.Rate = 0;
            return synthesizer;
        }

        public SpeechRecognitionEngine InitializeRecognizer(Grammar? grammar = null)
        {
            var recognizer = new SpeechRecognitionEngine(new CultureInfo("en-US"));
            recognizer.SetInputToDefaultAudioDevice();
            recognizer.LoadGrammar(grammar ?? new DictationGrammar());
            return recognizer;
        }
    }
}
