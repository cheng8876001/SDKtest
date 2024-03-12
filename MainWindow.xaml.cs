using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

namespace SDKtest
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        Test t = new Test();
        public MainWindow()
        {
            
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            t.Send();
        }
    }

    public  class Test : mscDLL
    {
        private string _loginParameters = "appid = ";
        private string _sessionBeginParameters = "sub = iat, domain = iat, language = zh_cn, accent = mandarin, sample_rate = 16000, result_type = json, result_encoding = utf-8, aue = raw";
        private string _filePath = "E:\\SystemData\\desktop\\recorded.wav";//录音文件路径

        public void Send()
        {
            ErrorCode errorCode = ErrorCode.MSP_SUCCESS;
            errorCode = (ErrorCode)MSPLogin(null, null, _loginParameters);//登录
            if(errorCode != ErrorCode.MSP_SUCCESS)
            {
                MessageBox.Show($"MSPLogin Error！\r\n Error Code：{errorCode}");
                MSPLogout();
                return;
            }
            IntPtr sessionID =  QISRSessionBegin(null, _sessionBeginParameters, ref errorCode);//开始一次语音识别，并传输各种参数
            if(errorCode != ErrorCode.MSP_SUCCESS)
            {
                MessageBox.Show($"QISRSessionBegin Error!\r\n Error Code：{errorCode}");
                MSPLogout();
                return;
            }

            byte[] arr = File.ReadAllBytes(_filePath);//读取文件所有内容
            if( arr == null )
            {
                return;
            }

            int pcmSize = arr.Length;//获取音频总长，并作为记录剩余
            int pcmCount = 0;
            int frameSize = 6400;//每次写入200ms音频(16k，16bit)：1帧音频20ms，10帧=200ms。16k采样率的16位音频，一帧的大小为640Byte
            AudioStatus audioStatus = AudioStatus.MSP_AUDIO_SAMPLE_CONTINUE;//记录音频发送状态
            EndPointerStatus endPointerStatus = EndPointerStatus.MSP_EP_LOOKING_FOR_SPEECH;//端点检测状态
            RecognitionStatus recognitionStatus = RecognitionStatus.MSP_REC_STATUS_SUCCESS;//识别器状态
            while (true)
            {
                if(pcmSize <= frameSize)
                {
                    frameSize = pcmSize;
                    if (frameSize <= 0)
                        break;
                }

                audioStatus = AudioStatus.MSP_AUDIO_SAMPLE_CONTINUE;
                if(pcmCount == 0)
                {
                    audioStatus = AudioStatus.MSP_AUDIO_SAMPLE_FIRST;//状态更新为第一次发送
                }

                byte[] buffer = new byte[frameSize];
                Array.Copy(arr,pcmCount, buffer, 0, frameSize);
                pcmCount += frameSize;
                pcmSize -= frameSize;

                errorCode = (ErrorCode)QISRAudioWrite(sessionID, buffer, (uint)frameSize, audioStatus, ref endPointerStatus, ref recognitionStatus);
                if (errorCode != ErrorCode.MSP_SUCCESS)
                {
                    MessageBox.Show($"QISRAudioWrite Error！Error Code：{errorCode}");
                    MSPLogout();
                    break;
                }

                /*
                if(recognitionStatus == RecognitionStatus.MSP_REC_STATUS_SUCCESS)//识别有结果了
                {
                    IntPtr rslt = QISRGetResult(sessionID, ref recognitionStatus, 0, ref errorCode);
                    if(errorCode != ErrorCode.MSP_SUCCESS)
                    {
                        MessageBox.Show($"QISRGetResult Error！Error Code：{errorCode}");
                        MSPLogout();
                        break;
                    }
                    if (rslt != null)
                    {
                       
                    }
                }
                */

                if (endPointerStatus == EndPointerStatus.MSP_EP_AFTER_SPEECH)
                    break;
                Thread.Sleep(10);//延时10ms
            }

            errorCode = (ErrorCode)QISRAudioWrite(sessionID,null,0,AudioStatus.MSP_AUDIO_SAMPLE_LAST, ref endPointerStatus, ref recognitionStatus);
            if(errorCode != ErrorCode.MSP_SUCCESS)
            {
                MessageBox.Show($"QISRAudioWrite Error！Error Code：{errorCode}");
                MSPLogout();
                return;
            }

            string str = null;
            while (recognitionStatus != RecognitionStatus.MSP_REC_STATUS_COMPLETE)
            {
                IntPtr rslt = QISRGetResult(sessionID, ref recognitionStatus, 0, ref errorCode);
                if (errorCode != ErrorCode.MSP_SUCCESS)
                {
                    MessageBox.Show($"QISRGetResult Error！Error Code：{errorCode}");
                    MSPLogout();
                    break;
                }
                str += IntPtr2Str(rslt);
                Thread.Sleep(10);
            }
            MessageBox.Show(str);
            QISRSessionEnd(sessionID,null);//结束本次识别
            MSPLogout();//注销
        }

        //对返回结果的IntPtr进行解析并提取识别结果
        private string IntPtr2Str(IntPtr ptr)
        {
            string uniStr = Marshal.PtrToStringUni(ptr);//将返回结果转换为Unicode编码
            string str = null;//用于记录识别结果
            if (uniStr != null)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(uniStr);
                string jsonData = Encoding.UTF8.GetString(bytes);//转为UTF8编码
                var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonData);
                var data = jsonObject["ws"];
                foreach (var item in data)
                {
                    foreach (var item2 in item["cw"])
                    {
                        str += item2["w"];
                    }
                }
            }
            return str;
        }
    }
}
