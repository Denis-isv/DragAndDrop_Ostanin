using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DragAndDrop_Ostanin
{
    public partial class CropPage : Page
    {
        private Point startPoint;
        private BitmapImage originalImage;
        private bool isDragging = false;

        public CropPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadImage();
        }

        private void LoadImageButton(object sender, RoutedEventArgs e)
        {
            LoadImage();
        }

        private void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "Image Files|*.jpg;*.png;*.bmp;*.jpeg";

            if (openDialog.ShowDialog() == true)
            {
                originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.UriSource = new Uri(openDialog.FileName);
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.EndInit();

                image.Source = originalImage;

                originalSize.Text = $"{originalImage.PixelWidth} x {originalImage.PixelHeight}";
                scaleSize.Text = $"{originalImage.PixelWidth} x {originalImage.PixelHeight}";
                outputSize.Text = "0 x 0";

                // Сбрасываем выделение
                ClearSelection();
            }

            // Пересчитываем размер Canvas после того, как WPF отрисует картинку
            Dispatcher.BeginInvoke(new Action(UpdateOverlaySize),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Синхронизирует размер Canvas с реальным отрисованным размером картинки.
        /// Благодаря этому координаты Canvas = координаты картинки (1:1).
        /// </summary>
        private void UpdateOverlaySize()
        {
            if (originalImage == null || image.ActualWidth <= 0 || image.ActualHeight <= 0)
                return;

            double controlW = image.ActualWidth;
            double controlH = image.ActualHeight;
            double imgAspect = originalImage.PixelWidth / (double)originalImage.PixelHeight;
            double ctrlAspect = controlW / controlH;

            if (imgAspect > ctrlAspect)
            {
                // Картинка шире -> по бокам будут пустые поля
                overlay.Width = controlW;
                overlay.Height = controlW / imgAspect;
            }
            else
            {
                // Картинка выше -> сверху/снизу будут пустые поля
                overlay.Height = controlH;
                overlay.Width = controlH * imgAspect;
            }
        }

        private void Image_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateOverlaySize();
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (originalImage == null) return;

            startPoint = e.GetPosition(overlay);
            isDragging = true;

            Canvas.SetLeft(cropRect, startPoint.X);
            Canvas.SetTop(cropRect, startPoint.Y);
            cropRect.Width = 0;
            cropRect.Height = 0;
            cropRect.Visibility = Visibility.Visible;

            overlay.CaptureMouse();
            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || originalImage == null) return;

            Point currentPoint = e.GetPosition(overlay);

            double left = Math.Min(startPoint.X, currentPoint.X);
            double top = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - startPoint.X);
            double height = Math.Abs(currentPoint.Y - startPoint.Y);

            Canvas.SetLeft(cropRect, left);
            Canvas.SetTop(cropRect, top);
            cropRect.Width = width;
            cropRect.Height = height;

            // Превью размера на выходе
            if (overlay.ActualWidth > 0)
            {
                double scale = originalImage.PixelWidth / overlay.ActualWidth;
                int outW = (int)Math.Round(width * scale);
                int outH = (int)Math.Round(height * scale);
                outputSize.Text = $"{outW} x {outH}";
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            if (overlay.IsMouseCaptured)
                overlay.ReleaseMouseCapture();
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ClearSelection();
            e.Handled = true;
        }

        private void ClearSelection()
        {
            cropRect.Visibility = Visibility.Collapsed;
            cropRect.Width = 0;
            cropRect.Height = 0;
            Canvas.SetLeft(cropRect, 0);
            Canvas.SetTop(cropRect, 0);
            outputSize.Text = "0 x 0";
            isDragging = false;
            if (overlay != null && overlay.IsMouseCaptured)
                overlay.ReleaseMouseCapture();
        }

        private void ClearCrop(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            e.Handled = true;
        }

        private void CropImage(object sender, RoutedEventArgs e)
        {
            if (originalImage == null)
            {
                MessageBox.Show("Сначала загрузите фото!");
                LoadImage();
                return;
            }

            if (cropRect.Width <= 0 || cropRect.Height <= 0 || cropRect.Visibility != Visibility.Visible)
            {
                MessageBox.Show("Выделите область для обрезки!");
                return;
            }

            if (overlay.ActualWidth <= 0) return;

            // Простой и точный пересчёт: Canvas имеет размер отрисованной картинки
            double scale = originalImage.PixelWidth / overlay.ActualWidth;

            double left = Canvas.GetLeft(cropRect);
            double top = Canvas.GetTop(cropRect);

            int x = (int)Math.Round(left * scale);
            int y = (int)Math.Round(top * scale);
            int w = (int)Math.Round(cropRect.Width * scale);
            int h = (int)Math.Round(cropRect.Height * scale);

            // Ограничиваем границами картинки
            x = Math.Max(0, Math.Min(x, originalImage.PixelWidth - 1));
            y = Math.Max(0, Math.Min(y, originalImage.PixelHeight - 1));
            w = Math.Min(w, originalImage.PixelWidth - x);
            h = Math.Min(h, originalImage.PixelHeight - y);

            if (w <= 0 || h <= 0)
            {
                MessageBox.Show("Область обрезки выходит за пределы изображения!");
                return;
            }

            try
            {
                CroppedBitmap cropped = new CroppedBitmap(originalImage, new Int32Rect(x, y, w, h));

                using (MemoryStream ms = new MemoryStream())
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(cropped));
                    encoder.Save(ms);
                    ms.Position = 0;

                    BitmapImage croppedImage = new BitmapImage();
                    croppedImage.BeginInit();
                    croppedImage.CacheOption = BitmapCacheOption.OnLoad;
                    croppedImage.StreamSource = ms;
                    croppedImage.EndInit();

                    image.Source = croppedImage;
                    originalImage = croppedImage;

                    originalSize.Text = $"{croppedImage.PixelWidth} x {croppedImage.PixelHeight}";
                    scaleSize.Text = $"{croppedImage.PixelWidth} x {croppedImage.PixelHeight}";
                }

                ClearSelection();
                Dispatcher.BeginInvoke(new Action(UpdateOverlaySize),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обрезки: {ex.Message}");
            }
        }

        private void BackToMain(object sender, RoutedEventArgs e)
        {
            MainWindow main = (MainWindow)Application.Current.MainWindow;
            main.ShowMainContent();
        }
    }
}