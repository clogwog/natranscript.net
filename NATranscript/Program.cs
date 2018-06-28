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
    using System.Xml;

    using System.Collections;
    using System.Collections.Generic;
    using System.Net;

    using NAudio.Wave;


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

        private readonly string rssfeeduri = @"http://feed.nashownotes.com/rss.xml";


        private int episodenumber;
        private string htmlOutputFilePath;
        private string opmlOutputFilePath;

        public class Episode
        {
            public string name;
            public string url;
            public string  episodeNumber = "";

            public bool IsEmpty
            {
                get
                {
                    return string.IsNullOrWhiteSpace(episodeNumber);
                }
            }
        }


        public Episode GetEpisodeFromRssFeed()
        {
            Episode retValue = new Episode();
            List<Episode> episodeList = new List<Episode>();
            // download rss feed
            XmlDocument feedXML = new XmlDocument();
            feedXML.Load(rssfeeduri);

            
            // show result
            XmlNodeList taglist = feedXML.GetElementsByTagName("item");
            int t = 1;
            foreach(XmlNode node in taglist)
            {
                Episode a = new Episode();
                a.name = node.SelectSingleNode("title")?.InnerText;
                a.url = node.SelectSingleNode("enclosure")?.Attributes?.GetNamedItem("url")?.InnerText;
                string[] subtitle = a.name.Split(':');
                if( subtitle.Length > 0)
                    a.episodeNumber = subtitle[0];

                Console.WriteLine(string.Format("{0}) {1}", t, a.name ));

                episodeList.Add(a);
                t++;
            }
            // ask to choose
            Console.WriteLine(string.Format("\n\nPlease select an episode\n"));
            string result = Console.ReadLine();
            if( int.TryParse(result, out int selection))
            {
                if( selection <= episodeList.Count)
                {
                    retValue = episodeList[selection - 1];
                }
            }

            // fill in retValue
            return retValue;

        }

        private string GetFileName(string hrefLink)
        {
            string[] parts = hrefLink.Split('/');
            string fileName = "";

            if (parts.Length > 0)
                fileName = parts[parts.Length - 1];
            else
                fileName = hrefLink;

            return fileName;
        }

        private static void ConvertMp3ToWav(string _inPath_, string _outPath_)
        {
            using (Mp3FileReader mp3 = new Mp3FileReader(_inPath_))
            {
                using (WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(mp3))
                {
                    WaveFileWriter.CreateWaveFile(_outPath_, pcm);
                }
            }
        }


        /// <summary>
        /// The entry point to this sample program. It validates the input arguments
        /// and sends a speech recognition request using the Microsoft.Bing.Speech APIs.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        public static void Main(string[] args)
        {

            if (args[0] == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                DisplayHelp("Please supply key");
                return;
            }

            Program ap = new Program();
            Episode selected = ap.GetEpisodeFromRssFeed();
            if( ! selected.IsEmpty)
            {
                // create a directory
                string newDirectory = @"episodes\" + selected.episodeNumber;
                System.IO.Directory.CreateDirectory(newDirectory);
                string fileName = ap.GetFileName(selected.url);
                string filePathName = newDirectory + @"\" + fileName;
                string wavfilePathName = filePathName.Replace(".mp3",".wav");

                // download file
                using (var client = new WebClient())
                {
                    client.DownloadProgressChanged += Client_DownloadProgressChanged;
                    client.DownloadFile(selected.url, filePathName);
                }

                // convert to wav because microsoft can't handle anything else....
                // i guess it is the same as flac for google
                ConvertMp3ToWav(filePathName, wavfilePathName);

                ap.Run(wavfilePathName, "en-US", LongDictationUrl, args[0], selected.episodeNumber).Wait();


            }
            
        }

        private static void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Write("\r Download Progess: {0}%", e.ProgressPercentage);
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

        public async Task Run(string audioFile, string locale, Uri serviceUrl, string subscriptionKey, string episode)
        {
            // create the preferences object
            var preferences = new Preferences(locale, serviceUrl, new CognitiveServicesAuthorizationProvider(subscriptionKey));


            string simpleFilename = Path.GetFileNameWithoutExtension(audioFile);
            
            if (int.TryParse(episode, out int outepisode))
                episodenumber = outepisode;
            else
                return;

            htmlOutputFilePath = Path.GetDirectoryName(audioFile) + @"\" + episodenumber.ToString() + ".html";
            opmlOutputFilePath = Path.GetDirectoryName(audioFile) + @"\" + episodenumber.ToString() + ".opml";

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
                    var requestMetadata = new RequestMetadata(Guid.NewGuid(), deviceMetadata, applicationMetadata, "NATranscriptService");

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
                message = "NATranscript.net Help";
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
