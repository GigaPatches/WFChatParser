﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IKeyboard
    {
        void SendEscape();
        void SendPaste(string text);
    }
}
