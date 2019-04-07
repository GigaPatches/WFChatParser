﻿using AdysTech.CredentialManager;
using Application;
using DataStream;
using ImageOCR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WarframeDriver;
using WFGameCapture;
using WFImageParser;

namespace ChatLoggerCLI
{
    public class Program
    {
        private static List<IDisposable> _disposables = new List<IDisposable>();
        private static CancellationTokenSource _cancellationSource;

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            
            var rivenParser = new RivenParser();
            _disposables.Add(rivenParser);
            //var c = new ChatImageCleaner(JsonConvert.DeserializeObject<CharInfo[]>("chars.json"));
            Console.WriteLine("Starting up image parser");
            var c = new ChatParser();
            //var t = new ImageParser();

            Console.WriteLine("Loading config for data sender");
            IConfiguration config = new ConfigurationBuilder()
              .AddJsonFile("appsettings.json", true, true)
              .AddJsonFile("appsettings.development.json", true, true)
              .AddJsonFile("appsettings.production.json", true, true)
              .Build();


            Console.WriteLine("Data sender connecting");
            var dataSender = new DataSender(new Uri(config["DataSender:HostName"]),
                config.GetSection("DataSender:ConnectionMessages").GetChildren().Select(i => i.Value),
                config["DataSender:MessagePrefix"],
                config["DataSender:DebugMessagePrefix"],
                true,
                config["DataSender:RawMessagePrefix"],
                config["DataSender:RedtextMessagePrefix"],
                config["DataSender:RivenImageMessagePrefix"]);

            dataSender.RequestToKill += (s, e) =>
            {
                Console_CancelKeyPress(null, null);
            };
            dataSender.RequestSaveAll += (s, e) =>
            {
                try
                {
                    for (int i = 6; i >= 0; i--)
                    {
                        var dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves");
                        if (e.Name != null && e.Name.Length > 0)
                            dir = Path.Combine(config["DEBUG:ImageDirectory"], "Saves", e.Name);
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        var curFile = Path.Combine(config["DEBUG:ImageDirectory"], "capture_" + i + ".png");
                        var copyFile = Path.Combine(dir, "capture_" + i + ".png");
                        if (File.Exists(curFile))
                            File.Copy(curFile, copyFile, true);
                    }
                }
                catch { }
            };

            var password = GetPassword(config["Credentials:Key"], config["Credentials:Salt"]);
            
            var gc = new GameCapture();
            var obs = GetObsSettings(config["Credentials:Key"], config["Credentials:Salt"]);
            var bot = new ChatRivenBot(config["LauncherPath"], new MouseHelper(),
                new ScreenStateHandler(),
                gc,
                obs,
                password,
                new KeyboardHelper(),
                new ChatParser(),
                dataSender,
                new RivenCleaner(),
                new RivenParserFactory(),
                new Application.LogParser.RedTextParser());

            _cancellationSource = new CancellationTokenSource();
            Task t =  Task.Run(() => bot.AsyncRun(_cancellationSource.Token));
            while(true)
            {
                if (t.IsFaulted || t.Exception != null)
                {
                    Console.WriteLine("\n" + t.Exception);
                    try
                    {
                        dataSender.AsyncSendDebugMessage(t.Exception.ToString()).Wait();
                        System.Threading.Thread.Sleep(2000);
                    }
                    catch
                    {
                        _cancellationSource.Cancel();
                    }
                    break;
                }
                else if (t.IsCompleted || t.IsCanceled || t.IsFaulted)
                    break;
                //var debug = progress.GetAwaiter().IsCompleted;
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static string GetPassword(string key, string salt)
        {
            var r = CredentialManager.GetCredentials("WFChatBot", CredentialManager.CredentialType.Generic);
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
        private static ObsSettings GetObsSettings(string key, string salt)
        {
            var r = CredentialManager.GetCredentials("OBS", CredentialManager.CredentialType.Generic);
            string password = null;
            string url = null;
            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.Password);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    password = Encoding.UTF8.GetString(ms.ToArray());
                }
            }

            using (MemoryStream ms = new MemoryStream())
            {
                PasswordDeriveBytes pdb = new PasswordDeriveBytes(key, Encoding.UTF8.GetBytes(salt));
                Aes aes = new AesManaged();
                aes.Key = pdb.GetBytes(aes.KeySize / 8);
                aes.IV = pdb.GetBytes(aes.BlockSize / 8);
                using (CryptoStream cs = new CryptoStream(ms,
                  aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    var input = Convert.FromBase64String(r.UserName);
                    cs.Write(input, 0, input.Length);
                    cs.Close();
                    url = Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            if (password != null && url != null)
                return new ObsSettings() { Url = url, Password = password };
            else
                return null;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _cancellationSource.Cancel();
            foreach (var item in _disposables)
            {
                if(item != null)
                    item.Dispose();
            }
            Console.WriteLine("Shutting down...");
        }
    }
}
