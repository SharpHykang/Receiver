using Receiver.Entity;
using Receiver.RabbitMQ;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
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

namespace Receiver
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        //定义消息队列
        private RabbitMqService _rabbitMqService;

        //使用可观察集合来绑定 DataGrid，当集合变化时，DataGrid自动更新。
        private ObservableCollection<Result> resultCollection;

        // 获取项目根目录
        private string projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../"));

        //处理接收到的消息
        string fileName = null;

        public MainWindow()
        {
            InitializeComponent();
            InitializeRabbitMq();
            InitializeDataGrid();
        }

        //初始化表格数据
        private void InitializeDataGrid()
        {
            resultCollection = new ObservableCollection<Result>();
            resultDataGrid.ItemsSource = resultCollection;
            using (var connection = GetConnection())
            {
                getResultList(connection);
            }
        }

        //连接RabbitMqService服务器
        private void InitializeRabbitMq()
        {
            _rabbitMqService = new RabbitMqService("localhost", "myQueue");
            //开始接收消息，并将收到的消息传递给 OnMessageReceived 方法
            _rabbitMqService.StartReceivingMessages(OnMessageReceived);
        }

        //获取队列消息
        private void OnMessageReceived(KeyValuePair<string, object> message)
        {
            string flag = message.Key;
            string info = (string)message.Value;
            if (flag == "status")   //消息为状态
            {
                if (fileName == null)
                {
                    MessageBox.Show("请先发送图片名！");
                    return;
                }
                //收消息时更新UI需要使用Dispatcher.Invoke，确保在UI线程上更新控件
                Dispatcher.Invoke(() =>
                {
                    status.Text = info;
                    if (info == "工作")  //读取图片
                    {
                        readImage();
                    }
                    else if (info == "完成")   //复制图片到result
                    {
                        copyImage();
                    }
                });
            }
            else  //消息为图片名
            {
                fileName = info;
            }
        }

        //复制图片
        private void copyImage()
        {
            // 资源的Pack URI
            Uri resourceUri = new Uri($"pack://application:,,,/Resources/original/{fileName}.jpg", UriKind.Absolute);
            // 获取资源流
            var resourceStream = Application.GetResourceStream(resourceUri);
            if (resourceStream != null)
            {
                // 构造目标路径
                string destinationFilePath = System.IO.Path.Combine(projectPath, "Resources\\result", $"{fileName}_copy.jpg");
                // 确保目标目录存在，不存在就创建：GetDirectoryName获取目录名
                string destinationDirectory = System.IO.Path.GetDirectoryName(destinationFilePath);
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                try
                {
                    // 使用using语句来管理资源流的生命周期，确保在使用完成后自动关闭和释放资源
                    using (var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write))
                    {
                        // 将资源流复制到目标文件
                        resourceStream.Stream.CopyTo(fileStream);
                    }
                    MessageBox.Show("图像已成功复制到：" + destinationFilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"图片复制失败: {ex.Message}！");
                }
            }
            else
            {
                MessageBox.Show("未获取到图片资源！");
            }
        }

        //读取图片
        private void readImage()
        {
            try
            {
                // 图像作为资源包含在项目中,使用Pack URI格式访问嵌入的资源：使用 Pack URI 加载资源文件
                Uri uri = new Uri($"pack://application:,,,/Resources/original/{fileName}.jpg");
                BitmapImage bitmap = new BitmapImage(uri);
                imageControl.Source = bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载图像: {ex.Message}！");
            }
        }

        //点击确定结果
        private void determine_click(object sender, RoutedEventArgs e)
        {
            if (fileName == null)
            {
                MessageBox.Show("请先发送图片名！");
                return;
            }
            bool isChecked = false;
            string resultTag = null;
            foreach (UIElement element in radioGroup.Children)
            {
                if (element is RadioButton radioButton && radioButton.IsChecked == true)
                {
                    isChecked = true;
                    resultTag = radioButton.Tag.ToString();
                    break;
                }
            }
            if (isChecked)
            {
                saveResult(resultTag);
            }
            else
            {
                MessageBox.Show("请选择结果！");
            }
        }

        //保存结果
        private void saveResult(string resultTag)
        {
            using (var connection = GetConnection())
            {
                //创建数据表
                string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS result (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                result TEXT CHECK(result IN ('OK', 'NG')),
                fileName TEXT NOT NULL
                );";
                using (var createTableCommand = new SQLiteCommand(createTableQuery, connection))
                {
                    createTableCommand.ExecuteNonQuery();
                }

                //保存数据
                string insertQuery = "INSERT INTO result (result, fileName) VALUES (@result, @fileName);";
                using (var insertCommand = new SQLiteCommand(insertQuery, connection))
                {
                    insertCommand.Parameters.AddWithValue("@result", resultTag);
                    insertCommand.Parameters.AddWithValue("@fileName", $"{fileName}.jpg");
                    insertCommand.ExecuteNonQuery();
                }
                MessageBox.Show("结果已保存到SQLite数据库！");
                //获取全部数据
                getResultList(connection);
            }
        }

        //获取全部数据
        private void getResultList(SQLiteConnection connection)
        {
            //清空数据
            resultCollection.Clear();
            string selectQuery = "SELECT id, result, fileName FROM result;";
            using (var selectCommand = new SQLiteCommand(selectQuery, connection))
            using (var reader = selectCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string result = reader.GetString(1);
                    string fileName = reader.GetString(2);
                    // 动态添加数据
                    resultCollection.Add(new Result { id = id, result = result, fileName = fileName });
                }
            }
        }

        // 通用连接函数：建立SQlite连接
        public SQLiteConnection GetConnection()
        {
            string databasePath = System.IO.Path.Combine(projectPath, "Database", "sqlite.db");
            string connectionString = $"Data Source={databasePath};Version=3;";
            var connection = new SQLiteConnection(connectionString);
            connection.Open();
            return connection;
        }

        //在窗口关闭时释放RabbitMQ连接资源。
        protected override void OnClosed(EventArgs e)
        {
            _rabbitMqService.Dispose();
            base.OnClosed(e);
        }
    }
}
