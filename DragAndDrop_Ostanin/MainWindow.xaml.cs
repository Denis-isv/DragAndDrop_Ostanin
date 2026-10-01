using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DragAndDrop_Ostanin
{
    public partial class MainWindow : Window
    {
        DispatcherTimer dispatcherTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 60);
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            image.Margin = new Thickness(Mouse.GetPosition(this).X - 25, Mouse.GetPosition(this).Y - 25, 0, 0);
        }

        private void image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            dispatcherTimer.Stop();
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            dispatcherTimer.Start();
        }

        public void ShowMainContent()
        {
            frame.Content = null;
        }

        private void OpenCropPage(object sender, RoutedEventArgs e)
        {
            frame.Navigate(new CropPage());
        }
    }
}