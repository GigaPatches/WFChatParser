﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Application.LineParseResult
{
    public class ChatMessageLineResult : BaseLineParseResult
    {
        public string Username { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string EnhancedMessage { get; set; }
        public List<ClickPoint> ClickPoints { get; set; }
        public void Append(ChatMessageLineResult lineParseResult)
        {
            this.RawMessage = this.RawMessage.Trim();
            lineParseResult.RawMessage = lineParseResult.RawMessage.Trim();
            this.EnhancedMessage = this.EnhancedMessage.Trim();
            lineParseResult.EnhancedMessage = lineParseResult.EnhancedMessage.Trim();

            this.RawMessage += " " + lineParseResult.RawMessage;
            var message = lineParseResult.EnhancedMessage;
            var addedRivens = 0;
            for (int i = 0; i < message.Length;)
            {
                if (message[i] == '[' && i + 1 < message.Length && Char.IsDigit(message[i + 1]))
                {
                    var id = Int32.Parse(message.Substring(i + 1, message.IndexOf(']', i + 1) - i - 1));
                    var newId = this.ClickPoints.Count + addedRivens;
                    message = message.Replace("[" + id + "]", "[" + newId + "]");
                    var p = lineParseResult.ClickPoints[addedRivens];
                    lineParseResult.ClickPoints[0] = new ClickPoint() { Index = this.EnhancedMessage.Length + i + 1, X = p.X, Y = p.Y };
                    i = i + ("[" + newId + "]").ToString().Length;
                    addedRivens++;
                }
                else
                    i++;
            }
            this.ClickPoints.AddRange(lineParseResult.ClickPoints);
            this.EnhancedMessage += " " + message;
        }

        public override string GetKey()
        {
            return Timestamp + Username;
        }

        public override bool KeyReady()
        {
            return this.Timestamp != string.Empty & this.Username != string.Empty;
        }
    }
}
