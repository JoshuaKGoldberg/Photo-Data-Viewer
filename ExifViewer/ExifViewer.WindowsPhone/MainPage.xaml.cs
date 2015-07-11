using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Jsbeautifier;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Newtonsoft.Json;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

namespace WindowsPhone
{
    public class ImageRecord
    {
        public BitmapImage Image { get; set; }

        public string AccessToken { get; set; }

        public string Path { get; set; }
    }

    public partial class MainPage : PhoneApplicationPage
    {
        public ImageRecord CurrentRecord { get; set; }

        public List<ImageRecord> RecentRecords { get; set; }

        private Beautifier beautifier = new Jsbeautifier.Beautifier();

        public MainPage()
        {
            RecentRecords = new List<ImageRecord>();
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var app = App.Current as App;
            if (app.FilePickerContinuationArgs != null)
            {
                this.ChoosePhoto_Click_Continue(app.FilePickerContinuationArgs);
                app.FilePickerContinuationArgs = null;
            }
        }

        private void ChoosePhoto_Click(object sender, RoutedEventArgs e)
        {
            Windows.Storage.Pickers.FileOpenPicker openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;

            // Filter for only allowing JPG/JPEG images
            openPicker.FileTypeFilter.Clear();
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".jpg");

            // Open the file picker, and wait for Application_ContractActivated
            openPicker.PickSingleFileAndContinue();
        }

        public void ChoosePhoto_Click_Continue(FileOpenPickerContinuationEventArgs args)
        {
            if (args.Files == null || args.Files.Count == 0)
            {
                return;
            }

            LoadFile(args.Files[0]);
        }

        private async void RecentPhoto_Tap(object sender, object e)
        {
            Image image = sender as Image;
            ImageRecord imageRecord = image.DataContext as ImageRecord;

            var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(imageRecord.AccessToken);
            LoadFile(file);
        }

        private async void LoadFile(StorageFile file)
        {
            // Make sure the file hasn't already been included
            ensureImageShownOnce(file);

            //// Open data stream for the selected file
            Stream fileStream = await file.OpenStreamForReadAsync();

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

            // Set the image as context for the rest of the currentGridChildren
            SetDataContext(image, file);
        }

        /// <returns>Whether the file already is selected</returns>
        private bool ensureImageShownOnce(StorageFile file)
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
            var photoDisplay = FindChildControl<Image>(ChoosePhotoSection, "MostRecentPhoto");
            photoDisplay.Source = newRecord.Image;

            if (CurrentRecord == null)
            {
                CurrentRecord = newRecord;
                var photoBackground = FindChildControl<Border>(ChoosePhotoSection, "MostRecentPhotoBackground");
                photoBackground.Visibility = Visibility.Visible;
                return;
            }

            RecentRecords.Insert(0, CurrentRecord);
            CurrentRecord = newRecord;

            var recentImages = FindChildControl<ItemsControl>(ChoosePhotoSection, "DisplayRecentImages");
            recentImages.ItemsSource = RecentRecords.ToArray();
        }

        private async void SetDataContext(BitmapImage image, StorageFile file)
        {
            var stream = await file.OpenStreamForReadAsync();
            var properties = await (await file.GetBasicPropertiesAsync()).RetrievePropertiesAsync(new string[] { });

            DataContext = new FileSummary(image, file, properties, stream);

            // Now that the data context is set, the panoramas should be visible
            BasicDataSection.Visibility = Visibility.Visible;
            ExifDataSection.Visibility = Visibility.Visible;
            FullDataSection.Visibility = Visibility.Visible;

            // The Save All button is always visible
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[1])).IsEnabled = true;
        }

        private void ListView_Changed(object sender, SelectionChangedEventArgs e)
        {
            List_SelectionChanged(sender, e);
        }

        private void List_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ListBox currentList = GetCurrentList();
            bool status = currentList != null && currentList.SelectedItem != null;
            ((ApplicationBarIconButton)(ApplicationBar.Buttons[0])).IsEnabled = status;
        }

        private void List_Copy(object sender, object e)
        {
            ListBox currentList = GetCurrentList();
            if (currentList != null && currentList.SelectedItem != null)
            {
                ExifDatum currentItem = currentList.SelectedItem as ExifDatum;
                Clipboard.SetText(currentItem.Name + ": " + currentItem.DisplayValue);
            }
        }

        private async void List_Save(object sender, object e)
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

            savePicker.PickSaveFileAndContinue();
        }

        private string DataLists_GetJsonString()
        {
            var BasicDataList = FindChildControl<ListBox>(BasicDataSection, "BasicDataList");
            var ExifDataList = FindChildControl<ListBox>(ExifDataSection, "ExifDataList");
            var FullDataList = FindChildControl<ListBox>(FullDataSection, "FullDataList");

            object[][] selectedItems = new object[][]
            {
                new object[BasicDataList.Items.Count],
                new object[ExifDataList.Items.Count],
                new object[FullDataList.Items.Count]
            };

            BasicDataList.Items.CopyTo(selectedItems[0], 0);
            ExifDataList.Items.CopyTo(selectedItems[1], 0);
            FullDataList.Items.CopyTo(selectedItems[2], 0);

            return beautifier.Beautify(JsonConvert.SerializeObject(selectedItems));
        }

        private ListBox GetCurrentList()
        {
            PanoramaItem currentSection = ContainerPanorama.SelectedItem as PanoramaItem;

            try
            {
                Grid currentGrid = currentSection.Content as Grid;
                ListBox currentList = currentGrid.Children[0] as ListBox;
                return currentList;
            }
            catch
            {
                return null;
            }
        }

        // http://stackoverflow.com/questions/22126659/how-to-access-any-control-inside-hubsection-datatemplate-in-windows-8-1-store
        private T FindChildControl<T>(DependencyObject control, string ctrlName)
            where T : DependencyObject
        {
            int numChildren = VisualTreeHelper.GetChildrenCount(control);

            for (int i = 0; i < numChildren; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(control, i);
                FrameworkElement fe = child as FrameworkElement;
                // Not a framework element or is null
                if (fe == null)
                {
                    return null;
                }

                if (child is T && fe.Name == ctrlName)
                {
                    // Found the control so return
                    return child as T;
                }
                else
                {
                    // Not found - search children
                    DependencyObject nextLevel = FindChildControl<T>(child, ctrlName);
                    if (nextLevel != null)
                    {
                        return nextLevel as T;
                    }
                }
            }

            return null;
        }

        private void NavigateToAbout(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.Relative));
        }
    }
}
