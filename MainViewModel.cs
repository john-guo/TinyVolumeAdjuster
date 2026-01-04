using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.Gui;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace TinyVolumeAdjuster
{
    partial class MainViewModel : ObservableObject
    {
        
        private VolumeAdjuster adjuster;


        [ObservableProperty]
        private ObservableCollection<VolumeItem> sessions;

        public MainViewModel() 
        {
            adjuster = new VolumeAdjuster();
            sessions = adjuster.GetItems();
        }
    }
}
