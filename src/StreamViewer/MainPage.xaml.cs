using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace StreamViewer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            try
            {
                var linksHistoryData = Plugin.Settings.CrossSettings.Current.GetValueOrDefault("History", "");
                if(linksHistoryData.Length > 0)
                {
                    linksHistory = JsonConvert.DeserializeObject<List<string>>(linksHistoryData);
                }
            }
            catch(Exception ex)
            {

            }
        }

        bool isStreamRunning = false;
        bool recordActive = false;
        bool resolveURL = true;
        StorageFolder saveFolder = null;
        StorageFolder recordFolder = null;
        string videoFile = "capture.jpg";
        double MaxTasksAtSameTime = 20;
        List<string> linksHistory = new List<string>();
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private async Task StreamLoad()
        {
            try
            {
                isStreamRunning = true;
                PreviewButton.Label = "Stop";
                PreviewButton.Icon = new SymbolIcon(Symbol.Stop);
                StreamLink.IsEnabled = false;
                recordButton.IsEnabled = true;
                StreamProgress.Visibility = Visibility.Visible;
                try
                {
                    cancellationTokenSource.Cancel();
                }
                catch (Exception ex)
                {

                }
                cancellationTokenSource = new CancellationTokenSource();
                if (StreamLink.Text.Trim().Length > 0)
                {
                    var streamURL = StreamLink.Text.Trim();
                    if (!linksHistory.Contains(streamURL))
                    {
                        linksHistory.Add(streamURL);
                        try
                        {
                            var data = JsonConvert.SerializeObject(linksHistory);
                            Plugin.Settings.CrossSettings.Current.AddOrUpdateValue("History", data);
                        }catch(Exception ex)
                        {

                        }
                    }
                    if (resolveURL && !streamURL.EndsWith("image.jpg"))
                    {
                        if (streamURL.Contains("?"))
                        {
                            if (streamURL.Contains("/?"))
                            {
                                streamURL = streamURL.Replace("?", "image.jpg?");
                            }
                            else
                            {
                                streamURL = streamURL.Replace("?", "/image.jpg?");
                            }
                        }
                        else
                        {
                            if (streamURL.EndsWith("/"))
                            {
                                streamURL += "image.jpg";
                            }
                            else
                            {
                                streamURL += "/image.jpg";
                            }
                        }
                    }
                    BlankPage.Visibility = Visibility.Collapsed;
                    BlankPageBack.Visibility = Visibility.Collapsed;
                    videoFile = null;
                    recordFolder = null;
                    try
                    {
                        if (saveFolder == null)
                        {
                            saveFolder = await CheckSaveFolder();
                        }
                    }
                    catch (Exception ex)
                    {

                    }

                    while (isStreamRunning)
                    {
                        try
                        {
                            var buffer = await GetImageAsByteArray(streamURL).AsAsyncOperation().AsTask(cancellationTokenSource.Token);
                            try
                            {
                                StreamInfo.Text = $"JPEG size: {((long)buffer.Length).ToFileSize()}";
                            }
                            catch (Exception ex)
                            {

                            }
                            try
                            {
                                if (recordActive && recordFolder != null)
                                {
                                    await BufferToFile(buffer, videoFile);
                                }
                            }
                            catch (Exception ex)
                            {

                            }
                            using (var stream = buffer.AsStream().AsRandomAccessStream())
                            {
                                var bitMap = new BitmapImage();
                                await bitMap.SetSourceAsync(stream);
                                Preview.Source = bitMap;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!ex.Message.Contains("TaskCanceledException"))
                            {
                                StreamInfo.Text = ex.Message;
                            }
                        }
                        try
                        {
                            await Task.Delay((int)(1000 / MaxTasksAtSameTime));
                        }
                        catch (Exception ex)
                        {
                            await Task.Delay(50);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowDialog(ex);
            }
            if (!introHidden)
            {
                BlankPage.Visibility = Visibility.Visible;
                BlankPageBack.Visibility = Visibility.Visible;
            }
            StreamProgress.Visibility = Visibility.Collapsed;
            isStreamRunning = false;
            PreviewButton.Label = "Start";
            PreviewButton.Icon = new SymbolIcon(Symbol.Play);
            StreamLink.IsEnabled = true;
            recordButton.IsEnabled = false;
            recordButton.IsChecked = false;
            recordActive = false;
            GC.Collect();
        }
        private async Task<IBuffer> GetImageAsByteArray(string urlImage)
        {
            var client = new HttpClient();
            //client.BaseAddress = new Uri(urlBase);
            var response = await client.GetAsync(new Uri(urlImage)).AsTask(cancellationTokenSource.Token); ;
            var stream = await response.Content.ReadAsBufferAsync();
            return stream;
        }

        private void Preview_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // handle the error
            ShowDialog(e.ErrorMessage);
        }
        private async void ShowDialog(Exception ex)
        {
            var messageDialog = new MessageDialog(ex.Message);
            messageDialog.Commands.Add(new UICommand(
                "Close"));
            await messageDialog.ShowAsync();
        }

        private async void ShowDialog(string message)
        {
            var messageDialog = new MessageDialog(message);
            messageDialog.Commands.Add(new UICommand(
                "Close"));
            await messageDialog.ShowAsync();
        }
        private async void TextBox_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            if (StreamLink.Text.Length > 0 && StreamLink.Text.StartsWith("http"))
            {
                PreviewButton.IsEnabled = true;
            }
            else
            {
                PreviewButton.IsEnabled = false;
            }
            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                //Set the ItemsSource to be your filtered dataset
                //sender.ItemsSource = dataset;
                if (linksHistory.Count > 0)
                {
                    ((AutoSuggestBox)sender).ItemsSource = linksHistory;
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (isStreamRunning)
            {
                isStreamRunning = false;
                cancellationTokenSource.Cancel();
            }
            else
            {
                await StreamLoad();
            }
        }

        private async Task BufferToFile(IBuffer buffer, string videoFile)
        {
            try
            {
                var videoStorageFile = await recordFolder.CreateFileAsync(videoFile, CreationCollisionOption.GenerateUniqueName);
                if (videoStorageFile != null)
                {
                    await FileIO.WriteBufferAsync(videoStorageFile, buffer);
                }
            }
            catch (Exception ex)
            {
                StreamInfo.Text = ex.Message;
            }
        }

        private async void CaptureImage_Clicked(object sender, RoutedEventArgs e)
        {
            await CaptureImage();
        }

        private async Task<StorageFolder> CheckSaveFolder()
        {
            try
            {
                var PicturesStorage = KnownFolders.PicturesLibrary;
                var SaveFolder = await PicturesStorage.CreateFolderAsync("MJPEGStreamer", CreationCollisionOption.OpenIfExists);
                if (SaveFolder != null)
                {
                    return SaveFolder;
                }
                else
                {

                    {
                        StreamInfo.Text = "Cannot access to pictures folder!";
                    }
                }
            }
            catch (Exception ex)
            {
                StreamInfo.Text = ex.Message;
            }
            return null;
        }

        private async Task CaptureImage(bool ShowConfirm = true)
        {
            try
            {
                var PicturesStorage = KnownFolders.PicturesLibrary;
                var SaveFolder = await PicturesStorage.CreateFolderAsync("MJPEGStreamer", CreationCollisionOption.OpenIfExists);
                if (SaveFolder != null)
                {
                    var _bitmap = new RenderTargetBitmap();
                    await _bitmap.RenderAsync(Preview);
                    var fileName = DateTime.Now.ToString().Replace("/", "_").Replace("\\", "_").Replace(":", "_").Replace(" ", "_") + ".jpg";
                    var savefile = await SaveFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    var pixels = await _bitmap.GetPixelsAsync();
                    using (IRandomAccessStream stream = await savefile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var encoder = await
                        BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                        byte[] bytes = pixels.ToArray();
                        BitmapAlphaMode mode = BitmapAlphaMode.Ignore;
                        if (Path.GetExtension(fileName).ToLower().Equals(".png"))
                        {
                            mode = BitmapAlphaMode.Straight;
                        }
                        encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                                mode,
                                                (uint)_bitmap.PixelWidth,
                                            (uint)_bitmap.PixelHeight,
                                                96,
                                                96,
                                                bytes);

                        await encoder.FlushAsync();
                    }
                    if (ShowConfirm)
                    {
                        ShowDialog("Image saved to Pictures -> MJPEGStreamer");
                    }
                }
                else
                {
                    if (ShowConfirm)
                    {
                        ShowDialog(new Exception("Cannot access to pictures folder!"));
                    }
                    else
                    {
                        StreamInfo.Text = "Cannot access to pictures folder!";
                    }
                }
            }
            catch (Exception ex)
            {
                StreamInfo.Text = ex.Message;
            }
        }
        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDialog("Simple MJPEG Stream Viewer\nDeveloped by Bashar Astifan");
        }

        bool introHidden = false;
        private void AppBarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (HideIntro.IsChecked.Value)
            {
                BlankPage.Visibility = Visibility.Collapsed;
                BlankPageBack.Visibility = Visibility.Collapsed;
                introHidden = true;
            }
            else
            {
                if (!isStreamRunning)
                {
                    BlankPage.Visibility = Visibility.Visible;
                    BlankPageBack.Visibility = Visibility.Visible;
                }
                introHidden = false;
            }
        }

        private async void recordButton_Click(object sender, RoutedEventArgs e)
        {
            if (recordActive && saveFolder != null)
            {
                var videoFileFolder = "capture";
                try
                {
                    videoFileFolder = DateTime.Now.ToString().Replace("/", "_").Replace("\\", "_").Replace(":", "_").Replace(" ", "_");
                    videoFile = videoFileFolder + ".jpg";
                }
                catch (Exception ex)
                {

                }
                recordFolder = await saveFolder.CreateFolderAsync(videoFileFolder, CreationCollisionOption.ReplaceExisting);
            }
        }

        private void AppBarToggleButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (ZoomButton.IsChecked.Value)
            {
                ImageContainer.ZoomMode = ZoomMode.Enabled;
                ImageContainer.MaxZoomFactor = 400;
                ImageContainer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                ImageContainer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                ImageContainer.ZoomMode = ZoomMode.Disabled;
                ImageContainer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                ImageContainer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
        }

        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            ShowDialog("How it works:\nThe stream link should be able to provide still images as this app will get image frame each request\n\nRecord:\nit will capture sequence images into one folder\nthen you can use W.U.T to convert the image to a video\n\nHistory:\nBy default links will be saved for easy access if you want to delete all the links reset the app data\n\nResolve URL:\nIf you have custom link other than MJPEG Streamer turn this options off");
        }

        private void AppBarButton_Click_2(object sender, RoutedEventArgs e)
        {
            ShowDialog("There is no collection at all not even for the error\nIf you have any privacy concerns please request the source code from the developer\n\nNote: Don't use this stream as secure stream there is no encryption used");
        }
    }
    public static class ExtensionMethods
    {
        public static string ToFileSize(this long l)
        {
            try
            {
                return String.Format(new FileSizeFormatProvider(), "{0:fs}", l);
            }
            catch (Exception e)
            {
                return "0 KB";
            }
        }
    }
    public class FileSizeFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter)) return this;
            return null;
        }

        private const string fileSizeFormat = "fs";
        private const Decimal OneKiloByte = 1024M;
        private const Decimal OneMegaByte = OneKiloByte * 1024M;
        private const Decimal OneGigaByte = OneMegaByte * 1024M;

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            try
            {
                if (format == null || !format.StartsWith(fileSizeFormat))
                {
                    return defaultFormat(format, arg, formatProvider);
                }
            }
            catch (Exception e)
            {

            }
            if (arg is string)
            {
                return defaultFormat(format, arg, formatProvider);
            }

            Decimal size;

            try
            {
                size = Convert.ToDecimal(arg);
            }
            catch (Exception e)
            {
                return defaultFormat(format, arg, formatProvider);
            }

            string suffix;
            if (size > OneGigaByte)
            {
                size /= OneGigaByte;
                suffix = " GB";
            }
            else if (size > OneMegaByte)
            {
                size /= OneMegaByte;
                suffix = " MB";
            }
            else if (size > OneKiloByte)
            {
                size /= OneKiloByte;
                suffix = " KB";
            }
            else
            {
                suffix = " B";
            }

            string precision = format.Substring(2);
            if (String.IsNullOrEmpty(precision)) precision = "2";
            return String.Format("{0:N" + precision + "}{1}", size, suffix);

        }

        private static string defaultFormat(string format, object arg, IFormatProvider formatProvider)
        {
            IFormattable formattableArg = arg as IFormattable;
            if (formattableArg != null)
            {
                return formattableArg.ToString(format, formatProvider);
            }
            return arg.ToString();
        }

    }
}
