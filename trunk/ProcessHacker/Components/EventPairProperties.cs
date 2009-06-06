﻿using System;
using System.Windows.Forms;
using ProcessHacker.Common; 
using ProcessHacker.Native.Objects;

namespace ProcessHacker.Components
{
    public partial class EventPairProperties : UserControl
    {
        private EventPairHandle _eventPairHandle;

        public EventPairProperties(EventPairHandle eventPairHandle)
        {
            InitializeComponent();

            _eventPairHandle = eventPairHandle;
        }

        private void buttonSetHigh_Click(object sender, EventArgs e)
        {
            try
            {
                _eventPairHandle.SetHigh();
            }
            catch (Exception ex)
            {
                PhUtils.ShowMessage(ex);
            }
        }

        private void buttonSetLow_Click(object sender, EventArgs e)
        {
            try
            {
                _eventPairHandle.SetLow();
            }
            catch (Exception ex)
            {
                PhUtils.ShowMessage(ex);
            }
        }
    }
}
