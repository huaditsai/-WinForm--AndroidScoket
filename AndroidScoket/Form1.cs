using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AndroidScoket
{
    public partial class Form1 : Form
    {
        bool isServerStart = false; //Server 狀態

        Thread processor; //實際處理
        ArrayList imgStreamArray; //影像stream

        public ArrayList usersArray = new ArrayList(); //使用者們
        public ArrayList userIDArray = new ArrayList(); //使用者們的ID

        TcpListener tcpListener;
        string serverIP;
        int port;

        Socket clientSocket; //使用者的連接
        Thread clientThread; //處理使用者
        Hashtable allClientSockets = new Hashtable(); //處理所有使用者的連接

        public Form1()
        {
            InitializeComponent();
        }

        public void StartServer()
        {
            try
            {
                if (isServerStart)
                    MessageBox.Show("Server Started");
                else
                {
                    processor = new Thread(new ThreadStart(StartListening));
                    processor.Start();
                    processor.IsBackground = true;

                    imgStreamArray = new ArrayList();
                    usersArray = new ArrayList();
                }

                btn_Start.Enabled = false;
                btn_Stop.Enabled = true;

                isServerStart = true;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StopServer()
        {
            try
            {
                if (isServerStart)
                {
                    tcpListener.Stop();
                    Thread.Sleep(500);
                    processor.Abort();

                    userIDArray.Clear();
                    usersArray.Clear();
                    allClientSockets.Clear();
                }
                else
                    MessageBox.Show("Server Stoped");

                btn_Start.Enabled = true;
                btn_Stop.Enabled = false;

                isServerStart = false;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private void StartListening()
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse(serverIP);
                tcpListener = new TcpListener(ipAddress, port); //IPAddress.Any 監聽所有的介面
                tcpListener.Start();
            }
            catch
            {
                MessageBox.Show("輸入的IP錯誤");
            }

            while (true)
            {
                //Thread.Sleep(50);

                try
                {
                    Socket socket = tcpListener.AcceptSocket(); //接受暫止連接要求
                    clientSocket = socket;

                    clientThread = new Thread(new ThreadStart(ProcessClient));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void ProcessClient()
        {
            Socket client = clientSocket;
            bool isAlive = true;

            while (isAlive)
            {
                Thread.Sleep(20);
                Byte[] buffer = null;
                bool isReceiveData = false;

                try
                {
                    buffer = new Byte[1024];
                    int count = client.Receive(buffer, SocketFlags.None); //接收使用者資料
                    //client.Send(System.Text.Encoding.UTF8.GetBytes("a"));
                    if (count > 0)
                        isReceiveData = true;
                }
                catch (Exception e)
                {
                    isAlive = false;
                    if (client.Connected)
                        client.Disconnect(true);
                    client.Close();

                    Console.WriteLine(e.Message);
                }

                if (!isReceiveData) //沒有接收到資料就關閉吧
                {
                    isAlive = false;
                    try
                    {
                        if (client.Connected)
                            client.Disconnect(true);
                        client.Close();
                    }
                    catch { }
                }

                string clientCommand = ""; //Android端會送出 (CONNECT/DISCONNECT/VIDEO)|使用者名|ID|視訊資料 
                clientCommand = System.Text.Encoding.UTF8.GetString(buffer);
                if (clientCommand.Contains("%7C"))
                    clientCommand = clientCommand.Replace("%7C", "|");

                //Console.WriteLine(clientCommand);

                string[] messages = clientCommand.Split('|');
                if (messages != null && messages.Length > 0) //若有接收到
                {
                    string status = messages[0]; //CONNECT / DISCONNECT / VIDEO
                    if (status == "CONNECT")
                    {
                        ClientConnected(messages, client);
                        Thread t = new Thread(new ThreadStart(RefreshUserList));
                        t.Start();
                    }
                    else if (status == "DISCONNECT")
                    {
                        ClientDisConnected(messages);
                        isAlive = false;
                        Thread t = new Thread(new ThreadStart(RefreshUserList));
                        t.Start();
                    }
                    else if (status == "VIDEO")
                    {
                        ReceiveClientData(messages, client, buffer);
                    }
                }
                else //沒有使用者發出的訊息
                {
                    client.Close();
                    isAlive = false;
                }
            }

        }

        private void ClientConnected(string[] messages, Socket client) //使用者連接上了
        {
            string devideID = messages[2].Trim();
            allClientSockets.Remove(messages[2]); //先刪除與此使用者之前的連接
            allClientSockets.Add(messages[2], client); //建立

            UserInfo user = new UserInfo(); //使用者資料
            user.UserName = messages[1].Trim();
            user.DeviceId = devideID;
            user.LoginTime = DateTime.Now;

            Socket tmpSocket = (Socket)allClientSockets[devideID];
            user.IPAddress = tmpSocket.RemoteEndPoint.ToString();

            int index = userIDArray.IndexOf(devideID);
            if (index >= 0)
            {
                userIDArray[index] = devideID;
                usersArray[index] = user;

                //MemoryStream stream = (MemoryStream)imgStreamArray[index];
                //if (stream != null)
                //{
                //    stream.Close();
                //    stream.Dispose();
                //}
            }
            else //增加一個欄位
            {
                userIDArray.Add(devideID);
                usersArray.Add(user);
                imgStreamArray.Add(null);
            }
        }

        private void ClientDisConnected(string[] messages) //使用者斷開了
        {
            string devideID = messages[2].Trim();
            RemoveUser(devideID); //把使用者從表中移除

            int index = userIDArray.IndexOf(devideID);
            if(index >= 0)
            {
                userIDArray.RemoveAt(index);
                usersArray.RemoveAt(index);
                //MemoryStream stream = (MemoryStream)imgStreamArray[index];
                //if (stream != null)
                //{
                //    stream.Close();
                //    stream.Dispose();
                //}
                imgStreamArray.RemoveAt(index);
            }

            Socket tmpSocket = (Socket)allClientSockets[devideID];
            if(tmpSocket != null)
            {
                tmpSocket.Close();
                allClientSockets.Remove(devideID);
            }
        }
        private static readonly DateTime Jan1st1970 = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }
        private void ReceiveClientData(string[] messages, Socket client, Byte[] buffer) //接收使用者資料
        {
            string devideID = messages[2].Trim(); // VIDEO|使用者名|ID|時間|視訊資料
            string headStr = messages[0] + "%7C" + messages[1] + "%7C" + messages[2] + "%7C" + messages[3] + "%7C"; //影像傳輸前的文字
            int headCunt = System.Text.Encoding.UTF8.GetByteCount(headStr); //影像傳輸前的文字數

            //Console.WriteLine(CurrentTimeMillis() - long.Parse(messages[3])); //計算時間差(延遲)

            MemoryStream stream = new MemoryStream(); //寫入記憶體
            if (stream.CanWrite)
            {
                stream.Write(buffer, headCunt, buffer.Length - headCunt);
                int len = -1;
                while ((len = client.Receive(buffer)) > 0) //之後接收到的都只有影像資料
                {
                    stream.Write(buffer, 0, len);
                }
            }
            stream.Flush();

            int index = userIDArray.IndexOf(devideID);
            if (index >= 0)
            {
                //MemoryStream stream2 = (MemoryStream)imgStreamArray[index];
                //if (stream != null)
                //{
                //    stream2.Close();
                //    stream2.Dispose();
                //}
                imgStreamArray[index] = stream;

                VideoForm videoForm = GetVideoForm(devideID);
                if (videoForm != null)
                    videoForm.DataStream = stream;
            }
        }

        private void RefreshUserList() //刷新使用者表
        {
            RefreshUser();
        }

        delegate void RefreshCallBack(); //進行對 Windows Form 控制項的安全執行緒呼叫
        private void RefreshUser() //進行對 Windows Form 控制項的安全執行緒呼叫
        {
            if (listView_Users.InvokeRequired)
            {
                RefreshCallBack d = new RefreshCallBack(RefreshUser);
                this.Invoke(d, new object[] { });
            }
            else
            {
                listView_Users.Items.Clear();

                UserInfo user;
                ListViewItem item;
                ListViewItem.ListViewSubItem subItem;
                Color color;
                Socket socket;

                if (usersArray != null && usersArray.Count > 0 && allClientSockets != null && allClientSockets.Count > 0)
                {
                    for (int i = 0; i < usersArray.Count; i++)
                    {
                        user = (UserInfo)usersArray[i];
                        socket = (Socket)allClientSockets[user.DeviceId];

                        item = listView_Users.Items.Add((i + 1).ToString());

                        if (user.Enable)
                            color = Color.Blue;
                        else
                            color = Color.Red;

                        item.ForeColor = color;

                        subItem = item.SubItems.Add(user.UserName);
                        subItem.ForeColor = color;

                        subItem = item.SubItems.Add(user.IPAddress);
                        subItem.ForeColor = color;

                        subItem = item.SubItems.Add(user.LoginTime.ToString());
                        subItem.ForeColor = color;
                    }
                }
                
            }
        }

        private void RemoveUser(string devideID) //把使用者從表中移除
        {
            int i = 0;
            if (usersArray != null && usersArray.Count > 0)
            {
                foreach (UserInfo user in usersArray)
                    if (user.DeviceId == devideID)
                    {
                        usersArray.Remove(i);
                        break;
                    }
                    else
                    {
                        i++;
                    }
            }
        }

        private int GetUserIndex(string userName)
        {
            int result = -1;
            string devideID = "";
            if (userIDArray != null && userIDArray.Count > 0)
            {
                int i = 0;
                foreach (UserInfo user in usersArray)
                    if (user.UserName == userName)
                    {
                        devideID = user.DeviceId;
                        break;
                    }
                    else
                    {
                        i++;
                    }

                i = 0;
                foreach (string id in userIDArray)
                    if (id == devideID)
                    {
                        result = i;
                        break;
                    }
                    else
                        i++;
            }
            return result;
        }

        private VideoForm GetVideoForm(string devideID)
        {
            VideoForm videoForm = null;
            foreach (Form form in this.OwnedForms)
                if(form is VideoForm)
                {
                    VideoForm tmp = form as VideoForm;
                    if(tmp.DeviceID == devideID)
                    {
                        videoForm = tmp;
                        break;
                    }
                }

            return videoForm;
        }

        private void btn_Start_Click(object sender, EventArgs e) //啟動
        {
            serverIP = text_ServerIP.Text;
            int tmpPort = 9527;

            if (int.TryParse(text_Port.Text, out tmpPort))
            {
                port = tmpPort;
                StartServer();
            }
            else
                MessageBox.Show("Error Port");            

        }

        private void btn_Stop_Click(object sender, EventArgs e)
        {
            StopServer(); //停止
            RefreshUserList(); //刷新使用者列表
        }

        private void listView_Users_MouseDoubleClick(object sender, MouseEventArgs e) //點兩下彈出視窗
        {
            if (listView_Users.SelectedItems != null && listView_Users.SelectedItems.Count > 0)
            {
                ListViewItem item = listView_Users.SelectedItems[0];
                string userName = item.SubItems[1].Text;
                int index = GetUserIndex(userName);
                if(index >=0)
                {
                    UserInfo user = (UserInfo)usersArray[index];
                    if (user != null)
                    {
                        VideoForm videoForm = new VideoForm(user);
                        this.AddOwnedForm(videoForm);
                        videoForm.Show();
                    }
                }

            }
        }

    }
}
