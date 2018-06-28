// ---------------------------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
// MIT LicensePermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//  </copyright>
//  ---------------------------------------------------------------------------------------------------------------------

namespace online.natranscribe
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using CognitiveServicesAuthorization;
    using Microsoft.Bing.Speech;

    /// <summary>
    /// This sample program shows how to use <see cref="SpeechClient"/> APIs to perform speech recognition.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Short phrase mode URL
        /// </summary>
        private static readonly Uri ShortPhraseUrl = new Uri(@"wss://speech.platform.bing.com/api/service/recognition");

        /// <summary>
        /// The long dictation URL
        /// </summary>
        private static readonly Uri LongDictationUrl = new Uri(@"wss://speech.platform.bing.com/api/service/recognition/continuous");

        /// <summary>
        /// A completed task
        /// </summary>
        private static readonly Task CompletedTask = Task.FromResult(true);

        /// <summary>
        /// Cancellation token used to stop sending the audio.
        /// </summary>
        private readonly CancellationTokenSource cts = new CancellationTokenSource();


        private int episodenumber;
        private string htmlOutputFilePath;
        private string opmlOutputFilePath;

        /// <summary>
        /// The entry point to this sample program. It validates the input arguments
        /// and sends a speech recognition request using the Microsoft.Bing.Speech APIs.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        public static void Main(string[] args)
        {
            // Validate the input arguments count.
            if (args.Length < 4)
            {
                DisplayHelp("Invalid number of arguments.");
                return;
            }

            // Ensure the audio file exists.
            if (!File.Exists(args[0]))
            {
                DisplayHelp("Audio file not found.");
                return;
            }

            if (!"long".Equals(args[2], StringComparison.OrdinalIgnoreCase) && !"short".Equals(args[2], StringComparison.OrdinalIgnoreCase))
            {
                DisplayHelp("Invalid RecognitionMode.");
                return;
            }

            // Send a speech recognition request for the audio.
            var p = new Program();
            p.Run(args[0], args[1], char.ToLower(args[2][0]) == 'l' ? LongDictationUrl : ShortPhraseUrl, args[3]).Wait();
        }

        /// <summary>
        /// Invoked when the speech client receives a partial recognition hypothesis from the server.
        /// </summary>
        /// <param name="args">The partial response recognition result.</param>
        /// <returns>
        /// A task
        /// </returns>
        public Task OnPartialResult(RecognitionPartialResult args)
        {
            Console.WriteLine("--- Partial result received by OnPartialResult ---");

            // Print the partial response recognition hypothesis.
            Console.WriteLine(args.DisplayText);

            Console.WriteLine();

            return CompletedTask;
        }

        /// <summary>
        /// Invoked when the speech client receives a phrase recognition result(s) from the server.
        /// </summary>
        /// <param name="args">The recognition result.</param>
        /// <returns>
        /// A task
        /// </returns>
        public Task OnRecognitionResult(RecognitionResult args)
        {
            var response = args;

            if (response.Phrases != null)
            {
                foreach (var result in response.Phrases)
                {
                    int seconds = (int)(result.MediaTime / 10000000);

                    string inline = result.DisplayText;
                    string[] words = inline.Split(new char[] { ' ' }, 2)
;
                    if (words.Length > 1)
                    {
                        string lineOutput = string.Format("<a target='naplayer' title='click to play' href='http://naplay.it/{0}/{1}'>{2}</a> {3}", episodenumber.ToString(), seconds, words[0], words[1]);
                        Console.WriteLine(lineOutput);
                        File.AppendAllLines(htmlOutputFilePath, new[] { lineOutput, " ", " " }); // add a few empty lines to split them up
                    }

                    break;
                }
            }

            return CompletedTask;
        }


        string GetEpisodeFromFile(string path)
        {
            string retValue = "";

            string pattern = "-[0-9]{4}-"; // look for 4 numbers surrounded by 2 dashes
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            MatchCollection matches = regex.Matches(path);
            if (matches.Count > 0)
            {
                retValue = matches[0].Value; // only get the first one
                Regex digitsOnly = new Regex(@"[^\d]"); // only keep the numbers
                retValue = digitsOnly.Replace(retValue, "");
            }
            return retValue;
        }


        /// <summary>
        /// Sends a speech recognition request to the speech service
        /// </summary>
        /// <param name="audioFile">The audio file.</param>
        /// <param name="locale">The locale.</param>
        /// <param name="serviceUrl">The service URL.</param>
        /// <param name="subscriptionKey">The subscription key.</param>
        /// <returns>
        /// A task
        /// </returns>
        /// 

        public async Task Run(string audioFile, string locale, Uri serviceUrl, string subscriptionKey)
        {
            // create the preferences object
            var preferences = new Preferences(locale, serviceUrl, new CognitiveServicesAuthorizationProvider(subscriptionKey));


            string simpleFilename = Path.GetFileNameWithoutExtension(audioFile);
            string episode = GetEpisodeFromFile(simpleFilename);

            if (int.TryParse(episode, out int outepisode))
                episodenumber = outepisode;
            else
                return;

            htmlOutputFilePath = Path.GetDirectoryName(audioFile) + "\\" + episodenumber.ToString() + ".html";
            opmlOutputFilePath = Path.GetDirectoryName(audioFile) + "\\" + episodenumber.ToString() + ".opml";

            // Create a a speech client
            using (var speechClient = new SpeechClient(preferences))
            {
                //speechClient.SubscribeToPartialResult(this.OnPartialResult);
                speechClient.SubscribeToRecognitionResult(this.OnRecognitionResult);

                // create an audio content and pass it a stream.
                using (var audio = new FileStream(audioFile, FileMode.Open, FileAccess.Read))
                {
                    var deviceMetadata = new DeviceMetadata(DeviceType.Near, DeviceFamily.Desktop, NetworkType.Ethernet, OsName.Windows, "1607", "Dell", "T3600");
                    var applicationMetadata = new ApplicationMetadata("NA Transcribe", "1.0.0");
                    var requestMetadata = new RequestMetadata(Guid.NewGuid(), deviceMetadata, applicationMetadata, "SampleAppService");

                    await speechClient.RecognizeAsync(new SpeechInput(audio, requestMetadata), this.cts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Display the list input arguments required by the program.
        /// </summary>
        /// <param name="message">The message.</param>
        private static void DisplayHelp(string message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = "SpeechClientSample Help";
            }

            Console.WriteLine(message);
            Console.WriteLine();
            Console.WriteLine("Arg[0]: Specify an input audio wav file.");
            Console.WriteLine("Arg[1]: Specify the audio locale.");
            Console.WriteLine("Arg[2]: Recognition mode [Short|Long].");
            Console.WriteLine("Arg[3]: Specify the subscription key to access the Speech Recognition Service.");
            Console.WriteLine();
            Console.WriteLine("Sign up at https://www.microsoft.com/cognitive-services/ with a client/subscription id to get a client secret key.");
        }
    }
}
