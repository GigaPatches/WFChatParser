﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Logger;
using static Application.ChatRivenBot;

namespace Application.Actionables.ChatBots
{
    public class TradeChatBot : IActionable
    {
        public bool IsRequestingControl => _requestingControl;
        private bool _requestingControl = true;

        private Task _controlTask = null;

        private ConcurrentQueue<RivenParseTaskWorkItem> _workQueue;
        private IRivenParser _rivenCropper;
        private CancellationToken _cancellationToken;
        private WarframeCredentials _warframeCredentials;
        private BotStates _currentState = BotStates.StartWarframe;

        private IMouse _mouse;
        private IKeyboard _keyboard;
        private IScreenStateHandler _screenStateHandler;
        private ILogger _logger;
        private IGameCapture _gameCapture;
        private Process _warframeProcess;

        public TradeChatBot(ConcurrentQueue<RivenParseTaskWorkItem> workQueue,
            IRivenParser rivenCropper,
            CancellationToken cancellationToken,
            WarframeCredentials warframeCredentials,
            IMouse mouse,
            IKeyboard keyboard,
            IScreenStateHandler screenStateHandler,
            ILogger logger,
            IGameCapture gameCapture)
        {
            _workQueue = workQueue;
            _rivenCropper = rivenCropper;
            _cancellationToken = cancellationToken;
            _warframeCredentials = warframeCredentials;
            _mouse = mouse;
            _keyboard = keyboard;
            _screenStateHandler = screenStateHandler;
            _logger = logger;
            _gameCapture = gameCapture;
            _warframeCredentials = warframeCredentials;
        }

        public Task TakeControl()
        {
            if (_warframeProcess == null || _warframeProcess.HasExited)
                _currentState = BotStates.StartWarframe;

            _requestingControl = false;

            switch (_currentState)
            {
                case BotStates.StartWarframe:
                    return StartWarframe();
                case BotStates.WaitForLoadScreen:
                    return WaitForLoadingScreen();
                case BotStates.LogIn:
                    return LogIn();
                case BotStates.ClaimReward:
                    return ClaimDailyRewardTask();
                case BotStates.CloseWarframe:
                    break;
                case BotStates.NavigateToChat:
                    break;
                case BotStates.ParseChat:
                    break;
                default:
                    break;
            }
            return Task.Delay(5000);
        }

        private enum BotStates
        {
            StartWarframe,
            WaitForLoadScreen,
            LogIn,
            ClaimReward,
            CloseWarframe,
            NavigateToChat,
            ParseChat
        }

        //private void Refactor()
        //{
        //    //Check if WF is running
        //    var wfAlreadyRunning = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0;
        //    if (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length == 0)
        //    {
        //        Log("Starting warframe");
        //        StartWarframe();
        //    }

        //    WaitForLoadingScreen(wfAlreadyRunning);

        //    //Check if on login screen
        //    LogIn();

        //    //Check if on daily reward screen
        //    ClaimDailyReward();

        //    //Wait 45 seconds for all of the notifications to clear out.
        //    if (!wfAlreadyRunning)
        //    {
        //        Log("Waiting for talking");
        //        Thread.Sleep(45 * 1000);
        //    }

        //    //Close any annoying windows it opened
        //    using (var screen = _gameCapture.GetFullImage())
        //    {
        //        if (_screenStateHandler.IsExitable(screen))
        //        {
        //            _mouse.Click(3816, 2013);
        //            Thread.Sleep(30);
        //        }
        //    }

        //    //Keep parsing chat as long as we are in a good state.
        //    var lastMessage = DateTime.Now;
        //    var firstParse = true;
        //    while (System.Diagnostics.Process.GetProcessesByName("Warframe.x64").Length > 0 && !cancellationToken.IsCancellationRequested)
        //    {
        //        if (_messageCache.Count > 5000)
        //        {
        //            lock (_messageCache)
        //            {
        //                lock (_messageCacheDetails)
        //                {
        //                    while (_messageCache.Count > 5000)
        //                    {
        //                        string key = null;
        //                        //Try to get the earliest key entered in
        //                        if (_messageCache.TryDequeue(out key) && key != null)
        //                        {
        //                            ChatMessageModel empty = null;
        //                            //If we fail to remove the detail item add the key back to the cache
        //                            if (!_messageCacheDetails.TryRemove(key, out empty))
        //                                _messageCache.Enqueue(key);
        //                        }
        //                    }
        //                }
        //            }
        //        }

        //        if (!firstParse)
        //            Log("Running loop");
        //        //Close and try again if no messages in 5 minutes
        //        if (DateTime.Now.Subtract(lastMessage).TotalMinutes > 5)
        //        {
        //            Log("Possible chat connection lost, closing WF");
        //            CloseWarframe();
        //            break;
        //        }

        //        //Try doing a parse
        //        SetForegroundWindow(Process.GetProcessesByName("Warframe.x64").First().MainWindowHandle);
        //        using (var screen = _gameCapture.GetFullImage())
        //        {
        //            _mouse.MoveTo(0, 0);
        //            Thread.Sleep(17);
        //            screen.Save("screen.png");
        //            var state = _screenStateHandler.GetScreenState(screen);

        //            //Check if we have some weird OK prompt (hotfixes, etc)
        //            if (_screenStateHandler.IsPromptOpen(screen))
        //            {
        //                Log("Unknown prompt detected. Closing.");
        //                _mouse.Click(screen.Width / 2, (int)(screen.Height * 0.57));
        //                Thread.Sleep(30);
        //                continue;
        //            }

        //            //If we somehow got off the glyph screen get back on it
        //            if (state != Enums.ScreenState.GlyphWindow)
        //            {
        //                Log("Going to glyph screen.");
        //                GoToGlyphScreenAndSetupFilters();
        //                Thread.Sleep(30);
        //                //In the event that we did not keep up with chat and ended up in a bad state we need to scroll to the bottom
        //                if (!firstParse)
        //                {
        //                    ScrollToBottomAndPause();
        //                }
        //                continue;
        //            }
        //            else if (state == ScreenState.GlyphWindow && _screenStateHandler.IsChatOpen(screen))
        //            {
        //                //Wait for the scroll bar before even trying to parse
        //                if (!_chatParser.IsScrollbarPresent(screen))
        //                {
        //                    Thread.Sleep(100);
        //                    continue;
        //                }

        //                //On first parse of a new instance scroll to the top
        //                if (firstParse && !wfAlreadyRunning)
        //                {
        //                    //Click top of scroll bar to pause chat
        //                    if (_chatParser.IsScrollbarPresent(screen))
        //                    {
        //                        Log("Scrollbar found. Starting.");
        //                        _mouse.MoveTo(3259, 658);
        //                        Thread.Sleep(33);
        //                        _mouse.Click(3259, 658);
        //                        Thread.Sleep(100);
        //                        firstParse = false;
        //                        continue;
        //                    }
        //                }
        //                //On first parse of existing image jump to bottom and pause
        //                else if (firstParse && wfAlreadyRunning)
        //                {
        //                    Log("Scrollbar found. Resuming.");
        //                    ScrollToBottomAndPause();
        //                    firstParse = false;
        //                    continue;
        //                }

        //                var sw = new Stopwatch();
        //                sw.Start();
        //                var chatLines = _chatParser.ParseChatImage(screen, true, true, 30);
        //                Log($"Found {chatLines.Length} new messages.");
        //                foreach (var line in chatLines)
        //                {
        //                    Log("Processing message: " + line.RawMessage);
        //                    lastMessage = DateTime.Now;
        //                    if (line is ChatMessageLineResult)
        //                    {
        //                        var processedCorrectly = ProcessChatMessageLineResult(cropper, line);
        //                        if (!processedCorrectly)
        //                        {
        //                            var path = SaveScreenToDebug(screen);
        //                            if (path != null)
        //                                _dataSender.AsyncSendDebugMessage("Failed to parse correctly. See: " + path);
        //                            _chatParser.InvalidCache(line.GetKey());
        //                            break;
        //                        }
        //                    }
        //                    else
        //                        Log("Unknown message: " + line.RawMessage);
        //                }
        //                Thread.Sleep(75);
        //                Log($"Processed (not riven parsed) {chatLines.Length} messages in : {sw.Elapsed.TotalSeconds} seconds");
        //                sw.Stop();
        //            }
        //            else
        //            {
        //                Log("Bad state detected! Restarting!!.");
        //                var path = SaveScreenToDebug(screen);
        //                _dataSender.AsyncSendDebugMessage("Bad state detected! Restarting!!. See: " + path);
        //                //We have no idea what state we are in. Kill the game and pray the next iteration has better luck.
        //                CloseWarframe();
        //                break;
        //            }
        //        }

        //        //Scroll down to get 27 more messages
        //        _mouse.MoveTo(3250, 768);
        //        Thread.Sleep(30);
        //        //Scroll down for new page of messages
        //        for (int i = 0; i < 27; i++)
        //        {
        //            _mouse.ScrollDown();
        //            Thread.Sleep(17);
        //        }
        //        for (int i = 0; i < 1; i++)
        //        {
        //            _mouse.ScrollUp();//Pause chat
        //            Thread.Sleep(90);
        //        }
        //        _mouse.MoveTo(0, 0);
        //        Thread.Sleep(17);
        //    }
        //}

        private async Task StartWarframe()
        {
            _requestingControl = false;
            var existingWarframes = System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray();

            var launcher = new System.Diagnostics.Process()
            {
                StartInfo = _warframeCredentials.StartInfo
            };
            launcher.Start();
            await Task.Delay(5000);

            ////If not start launcher, click play until WF starts
            while (true)
            {
                _screenStateHandler.GiveWindowFocus(launcher.MainWindowHandle);
                var launcherRect = _screenStateHandler.GetWindowRectangle(launcher.MainWindowHandle);
                _mouse.Click(launcherRect.Left + (int)((launcherRect.Right - launcherRect.Left) * 0.7339181286549708f),
                    launcherRect.Top + (int)((launcherRect.Bottom - launcherRect.Top) * 0.9252336448598131f));
                await Task.Delay(17);
                _keyboard.SendSpace();
                await Task.Delay(1000);
                if (launcher.HasExited)
                {
                    await Task.Delay(5000);
                    break;
                }
            }

            foreach (var warframe in System.Diagnostics.Process.GetProcessesByName("Warframe.x64").ToArray())
            {
                if (!existingWarframes.Any(eWF => eWF.MainWindowHandle == warframe.MainWindowHandle))
                    _warframeProcess = warframe;
            }

            _currentState = BotStates.WaitForLoadScreen;
            _requestingControl = true;
        }

        private async Task WaitForLoadingScreen()
        {
            _requestingControl = false;

            _logger.Log("Waiting for login screen");
            var startTime = DateTime.Now;
            //We may have missed the loading screen. If we started WF then wait even longer to get to the login screen
            var atLogin = false;
            while (DateTime.Now.Subtract(startTime).TotalMinutes < 1)
            {
                _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
                _mouse.MoveTo(0, 0);
                using (var screen = _gameCapture.GetFullImage())
                {
                    screen.Save("screen.png");
                    if (_screenStateHandler.GetScreenState(screen) != Enums.ScreenState.LoginScreen)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        atLogin = true;
                        break;
                    }
                }
            }

            if (atLogin)
            {
                _currentState = BotStates.LogIn;
            }
            else
                _currentState = BotStates.WaitForLoadScreen;
            _requestingControl = true;
        }

        private async Task LogIn()
        {
            _screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle);
            _mouse.Click(0, 0);
            await Task.Delay(17);

            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                if (_screenStateHandler.GetScreenState(screen) == Enums.ScreenState.LoginScreen)
                {
                    _logger.Log("Logging in");
                    ////Username
                    //_mouse.Click(3041, 1145);
                    //await Task.Delay(17);
                    //_keyboard.SendPaste(_warframeCredentials.Username);
                    //await Task.Delay(17);

                    //Password
                    _mouse.Click(2936, 1235);
                    await Task.Delay(17);
                    _keyboard.SendPaste(_warframeCredentials.Password);
                    await Task.Delay(17);

                    //Login
                    _mouse.Click(2945, 1333);

                    //Give plenty of time for the screen to transition
                    await Task.Delay(5000);
                }
            }

            ClaimDailyReward();
            await Task.Delay(1000);

            _currentState = BotStates.NavigateToChat;
            _requestingControl = true;
        }

        private void ClaimDailyReward()
        {
            if (_screenStateHandler.GiveWindowFocus(_warframeProcess.MainWindowHandle))
                _mouse.Click(0, 0);

            using (var screen = _gameCapture.GetFullImage())
            {
                screen.Save("screen.png");
                var state = _screenStateHandler.GetScreenState(screen);
                if (state == Enums.ScreenState.DailyRewardScreenItem)
                {
                    _logger.Log("Claiming random middle reward");
                    _mouse.Click(2908, 1592);
                }
                else if (state == Enums.ScreenState.DailyRewardScreenPlat)
                {
                    _logger.Log("Claiming unkown plat discount");
                    _mouse.Click(3325, 1951);
                }
            }
        }

        private Task ClaimDailyRewardTask()
        {
            return Task.Run(() =>
            {
                ClaimDailyReward();
                _currentState = BotStates.NavigateToChat;
                _requestingControl = true;
            });
        }
    }
}
