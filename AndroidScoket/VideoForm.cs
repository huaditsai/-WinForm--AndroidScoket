using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AndroidScoket
{
    public partial class VideoForm : Form
    {
        public string DeviceID;
        public VideoForm(UserInfo user)
        {
            InitializeComponent();
            DeviceID = user.DeviceId;

            this.Text = user.UserName + " [" + user.IPAddress + "]";
        }

        public MemoryStream DataStream
        { 
            set
            {
                try
                {
                    if (value != null)
                    {
                        pictureBox1.Image = Bitmap.FromStream(value);
                        value.Close();
                    }
                }
                catch { }
            }
        }

        private void VideoForm_KeyDown(object sender, KeyEventArgs e) //關閉視窗
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }



    }
}
