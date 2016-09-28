﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevComponents.DotNetBar;

namespace WzComparerR2.LuaConsole
{
    public partial class FrmLuaEditor : DevComponents.DotNetBar.OfficeForm
    {
        public FrmLuaEditor()
        {
            InitializeComponent();
            textEditorControl1.SetHighlighting("Lua");
        }

        public void LoadFile(string fileName)
        {

        }

        public string FileName { get; private set; }

        public string CodeContent
        {
            get { return this.textEditorControl1.Text; }
        }
    }
}