using HearthDb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Net.Http;
using System.Collections.ObjectModel;
using HearthDb.Enums;
using System.Windows.Forms;

namespace CardSearcher
{
    /// <summary>
    /// SearchWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SearchWindow 
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private ObservableCollection<CardResult> cardResults;
        private CardSearcher cardSearcher;

        public SearchWindow()
        {
            InitializeComponent();
            InputBox.TextChanged += InputBox_TextChanged;
            cardResults = new ObservableCollection<CardResult>();
            ResultsListView.ItemsSource = cardResults;
            cardSearcher = new CardSearcher();
            Task.Run(async () => await cardSearcher.Run()); // 使用 Task.Run 来处理异步调用
        }

        // async method 
        private async void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string tmp_Id = InputBox.Text;
            if (tmp_Id.Length >= 3)
            {
                // 获取匹配的卡片
                var resultList = new List<Card>();
                bool is_bg = tmp_Id.StartsWith("bg");
                bool is_number = tmp_Id.All(char.IsDigit);
                // tmp_Id is start with "bg" 
                if (is_bg){
                    resultList = await Task.Run(() => cardSearcher.GetBaconCards()
                        .Where(kvp => kvp.Id.ToLower().Contains(tmp_Id.ToLower()))
                        .ToList());
                }
                // tmp_Id is number
                else if (is_number){
                    resultList = await Task.Run(() => cardSearcher.GetBaconCards()
                        .Where(kvp => kvp.DbfId.ToString().Contains(tmp_Id))
                        .ToList());
                }
                var cardDataList = cardSearcher.CardDataList;
                // 输出结果数量到控制台
                Console.WriteLine($"resultList.count: {resultList.Count}");

                // 清空之前的结果
                cardResults.Clear();

                // 遍历结果并下载图片
                foreach (var tmpCard in resultList)
                {
                    Console.WriteLine($"tmpCard.Id: {tmpCard.Id}");
                    var image = await GetCardImageAsync(tmpCard.Id); // 获取卡片图片
                    var tags = new List<string>();
                    var keywords = new List<string>();
                    var races = new List<string>();
                    if (is_bg){
                        // wikitags if wikiTagsList is null, create a new list
                        tags = cardDataList.Find(card => card.id == tmpCard.Id).wikiTagsList ?? new List<string>();
                        // add keywordsList if keywordsList is not null
                        keywords = cardDataList.Find(card => card.id == tmpCard.Id).KeywordsList;
                        races = cardDataList.Find(card => card.id == tmpCard.Id).RacesList;
                    }
                    else if (is_number){
                        tags = cardDataList.Find(card => card.dbfId == tmpCard.DbfId.ToString()).wikiTagsList ?? new List<string>();
                        keywords = cardDataList.Find(card => card.dbfId == tmpCard.DbfId.ToString()).KeywordsList;
                        races = cardDataList.Find(card => card.dbfId == tmpCard.DbfId.ToString()).RacesList;
                    }
                    if (keywords != null)
                    {
                        tags.AddRange(keywords);
                    }
                    cardResults.Add(new CardResult { ImageSource = image, DisplayText = tmpCard.GetLocName(Locale.zhCN), Tags = tags, Races = races }); // 添加到集合
                }
            }
        }

        private async Task<BitmapImage> GetCardImageAsync(string cardId)
        {
            // 如果image文件夹不存在，则创建
            if (!Directory.Exists("image"))
            {
                Directory.CreateDirectory("image");
            }

            string imagePath = System.IO.Path.Combine("image", $"{cardId}.jpg");

            // 检查文件是否存在
            if (File.Exists(imagePath))
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 确保图像在加载后可用
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // 使图像可以在不同线程中使用
                return bitmapImage;
            }
            else
            {
                // 下载图片并保存
                var url = $"https://static.zerotoheroes.com/hearthstone/cardart/256x/{cardId}.jpg";
                var imageBytes = await httpClient.GetByteArrayAsync(url);

                // 使用 FileStream 异步写入文件
                using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await fileStream.WriteAsync(imageBytes, 0, imageBytes.Length);
                }

                return await GetCardImageAsync(cardId); // 递归调用以获取新下载的图像
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string input = InputBox.Text;
            // Perform search action with the input value
        }

        private void Image_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                // 创建一个新的窗口来显示放大的图片
                var popup = new Window
                {
                    Width = 200,
                    Height = 200,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    Content = new Image
                    {
                        Source = image.Source,
                        Stretch = Stretch.Uniform,
                        Width = 200,
                        Height = 200
                    }
                };

                // 获取图片在屏幕上的位置
                var position = image.PointToScreen(new Point(0, 0));
                popup.Left = (position.X + (image.ActualWidth / 2) - (popup.Width / 2)) / 2 + 10;
                popup.Top = (position.Y + image.ActualHeight)/2 + 35;

                popup.Show();
                image.Tag = popup; // 保存窗口引用
            }
        }

        private void Image_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var image = sender as Image;
            if (image != null && image.Tag is Window popup)
            {
                popup.Close(); // 关闭放大窗口
                image.Tag = null; // 清除引用
            }
        }

        private void Tags_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                var clickedTag = (string)((FrameworkElement)e.OriginalSource).DataContext;
                //ControlA.Text += clickedTag + " "; // 将点击的标签添加到控件A中
            }
        }

        private void Races_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                var clickedRace = (string)((FrameworkElement)e.OriginalSource).DataContext;
                //ControlA.Text += clickedRace + " "; // 将点击的种族添加到控件A中
            }
        }
    }

    // 用于存储卡片结果的类
    public class CardResult
    {
        public BitmapImage ImageSource { get; set; }
        public string DisplayText { get; set; }

        // 用于存储标签的列表
        public List<string> Tags { get; set; } = new List<string>(); // 初始化为一个空列表
        public List<string> Races { get; set; } = new List<string>(); // 初始化为一个空列表
    }
}
