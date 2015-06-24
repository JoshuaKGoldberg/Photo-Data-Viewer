using System;
using System.Collections.Generic;
using System.IO;
using Jsbeautifier;
using Newtonsoft.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace PhotoDataViewer
{
    public sealed partial class MainPage : Page
    {
        public ImageRecord CurrentRecord { get; set; }

        public List<ImageRecord> RecentRecords { get; set; }

        public MainPage()
        {
            RecentRecords = new List<ImageRecord>();
            InitializeComponent();
        }

        private async void ChoosePhoto_Click(object sender, RoutedEventArgs e)
        {
            Windows.Storage.Pickers.FileOpenPicker openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;

            // Filter for only allowing JPG/JPEG images
            openPicker.FileTypeFilter.Clear();
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".jpg");

            // Open the file picker
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            LoadFile(file);
        }

        private async void ListPhoto_Click(object sender, PointerRoutedEventArgs e)
        {
            Image image = sender as Image;
            ImageRecord imageRecord = image.DataContext as ImageRecord;

            LoadFile(await StorageApplicationPermissions.FutureAccessList.GetFileAsync(imageRecord.AccessToken));
        }

        private async void LoadFile(StorageFile file)
        {
            // Make sure the file hasn't already been included
            EnsureImageShownOnce(file);

            // Open data stream for the selected file
            IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);

            // Set the image source to the selected bitmap
            BitmapImage image = new BitmapImage();
            image.SetSource(fileStream);

            // Add picked file to RecentRecords for later access
            RecordImageRequest(new ImageRecord
                {
                    AccessToken = StorageApplicationPermissions.FutureAccessList.Add(file),
                    Image = image,
                    Path = file.Path
                });

            // Set the image as context for the rest of the stuff
            SetDataContext(image, file);
        }

        /// <returns>Whether the file already is selected</returns>
        private bool EnsureImageShownOnce(StorageFile file)
        {
            for (int i = 0; i < RecentRecords.Count; i += 1)
            {
                if (RecentRecords[i].Path == file.Path)
                {
                    RecentRecords.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private void RecordImageRequest(ImageRecord newRecord)
        {
            MostRecentPhoto.Source = newRecord.Image;

            if (CurrentRecord == null)
            {
                CurrentRecord = newRecord;
                MostRecentPhotoBackground.Visibility = Visibility.Visible;
                return;
            }

            RecentRecords.Insert(0, CurrentRecord);
            CurrentRecord = newRecord;

            displayRecentImages.ItemsSource = RecentRecords.ToArray();
        }

        private async void SetDataContext(BitmapImage image, StorageFile file)
        {
            var stream = await file.OpenAsync(FileAccessMode.Read);
            var properties = await (await file.GetBasicPropertiesAsync()).RetrievePropertiesAsync(new string[] { });

            DataContext = new FileSummary(image, file, properties, stream);

            // Show the relevant sub-headers
            if (SubHeaderBasic.Visibility != Visibility.Visible)
            {
                SubHeaderBasic.Visibility = Visibility.Visible;
                SubHeaderExif.Visibility = Visibility.Visible;
                SubHeaderFull.Visibility = Visibility.Visible;
            }
        }

        private void BasicDataList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            BasicDataClearButton.Visibility = BasicDataList.SelectedItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            DataLists_SelectionChanged(sender, e);
        }

        private void BasicDataList_SelectAll(object sender, RoutedEventArgs e)
        {
            BasicDataList.SelectAll();
        }

        private void BasicDataList_Clear(object sender, RoutedEventArgs e)
        {
            BasicDataList.SelectedItems.Clear();
        }

        private void ExifDataList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ExifDataClearButton.Visibility = ExifDataList.SelectedItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            DataLists_SelectionChanged(sender, e);
        }

        private void ExifDataList_SelectAll(object sender, RoutedEventArgs e)
        {
            ExifDataList.SelectAll();
        }

        private void ExifDataList_Clear(object sender, RoutedEventArgs e)
        {
            ExifDataList.SelectedItems.Clear();
        }

        private void FullDataList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            FullDataClearButton.Visibility = FullDataList.SelectedItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            DataLists_SelectionChanged(sender, e);
        }

        private void FullDataList_SelectAll(object sender, RoutedEventArgs e)
        {
            FullDataList.SelectAll();
        }

        private void FullDataList_Clear(object sender, RoutedEventArgs e)
        {
            FullDataList.SelectedItems.Clear();
        }

        private void DataLists_SelectionChanged(object sender, RoutedEventArgs e)
        {
            int numSelected = 0;
            Visibility visibility;

            numSelected += BasicDataList.SelectedItems.Count;
            numSelected += ExifDataList.SelectedItems.Count;
            numSelected += FullDataList.SelectedItems.Count;

            visibility = numSelected == 0 ? Visibility.Collapsed : Visibility.Visible;
            ButtonClear.Visibility = visibility;
            ButtonCopy.Visibility = visibility;
        }

        private string DataLists_GetJsonString()
        {
            object[][] selectedItems = new object[][]
            {
                new object[BasicDataList.SelectedItems.Count],
                new object[ExifDataList.SelectedItems.Count],
                new object[FullDataList.SelectedItems.Count]
            };

            BasicDataList.SelectedItems.CopyTo(selectedItems[0], 0);
            ExifDataList.SelectedItems.CopyTo(selectedItems[1], 0);
            FullDataList.SelectedItems.CopyTo(selectedItems[2], 0);

            Beautifier beautifier = new Jsbeautifier.Beautifier();

            return beautifier.Beautify(JsonConvert.SerializeObject(selectedItems));
        }

        private void AppBarButton_Clear(object sender, RoutedEventArgs e)
        {
            BasicDataList.SelectedItems.Clear();
            ExifDataList.SelectedItems.Clear();
            FullDataList.SelectedItems.Clear();

            BottomAppBar.IsOpen = false;
        }

        private void AppBarButton_SelectAll(object sender, RoutedEventArgs e)
        {
            BasicDataList.SelectAll();
            ExifDataList.SelectAll();
            FullDataList.SelectAll();
        }

        private void AppBarButton_Copy(object sender, RoutedEventArgs e)
        {
            DataPackage dp = new DataPackage();
            dp.SetText(DataLists_GetJsonString());
            Clipboard.SetContent(dp);
        }

        private async void AppBarButton_Save(object sender, RoutedEventArgs e)
        {
            string now = DateTime.Now.ToString();
            string text = DataLists_GetJsonString();
            string fileName = Application.Current.Resources["AppName"] + " " + now.Replace('/', '-').Replace(':', '.') + ".json";
            byte[] buffer = System.Text.UTF8Encoding.UTF8.GetBytes(text);

            StorageFile file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName);
            await FileIO.WriteBytesAsync(file, buffer);

            FileSavePicker savePicker = new FileSavePicker();
            savePicker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });
            savePicker.SuggestedSaveFile = file;
            savePicker.SuggestedFileName = file.Name;
            
            StorageFile fileSaved = await savePicker.PickSaveFileAsync();
            if (fileSaved != null)
            {
                CachedFileManager.DeferUpdates(fileSaved);
                await FileIO.WriteBytesAsync(fileSaved, buffer);
                await CachedFileManager.CompleteUpdatesAsync(file);
            }
        }
    }
}
