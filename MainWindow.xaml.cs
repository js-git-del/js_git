using System;
using System.Windows;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using OfficeOpenXml;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using iTextSharp.text;
using iTextSharp.text.pdf;


namespace wpf_ironpython
{
    public partial class MainWindow : Window
    {
        private ScriptEngine engine;
        private ScriptScope scope;   
        private static readonly HttpClient client = new HttpClient();
        private const string OpenAiApiKey = "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";

        public MainWindow()
        {
            InitializeComponent();
            InitializePython();
            InitializeHttpClient();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        }
        private void UpdateUI(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateUI(message));
                return;
            }

            ChatOutputTextBox.AppendText(message + Environment.NewLine);
            ChatOutputTextBox.ScrollToEnd();
        }

        private void InitializePython()
        {
            try
            {
                engine = Python.CreateEngine();
                scope = engine.CreateScope();

                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chatbot.py");
                engine.ExecuteFile(scriptPath, scope);
                ChatOutputTextBox.Text += "Python script loaded successfully.\n";

                // 함수 존재 여부 확인
                var scrapeFunc = scope.GetVariable("execute_scrape_news");
                var emailFunc = scope.GetVariable("execute_send_email_with_excel");
                if (scrapeFunc == null || emailFunc == null)
                {
                    ChatOutputTextBox.Text += "Warning: One or more required Python functions not found.\n";
                }
            }
            catch (Exception ex)
            {
                ChatOutputTextBox.Text += $"Error initializing Python: {ex.Message}\n";
                ChatOutputTextBox.Text += $"Stack Trace: {ex.StackTrace}\n";
            }
        }
        private void InitializeHttpClient()
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OpenAiApiKey);
        }
        private async Task<string> GetChatbotResponseAsync(string input)
        {
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
            new { role = "system", content = "You are a helpful assistant." },
            new { role = "user", content = input }
        }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonResponse = JObject.Parse(responseBody);

            return jsonResponse["choices"][0]["message"]["content"].ToString();
        }


        private string GeneratePdfReport(string reportContent, string keyword)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string fileName = $"{keyword}_report_{timestamp}.pdf";
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            try
            {
                // 한글 폰트 경로 설정
                string fontPath = @"C:\Windows\Fonts\malgun.ttf";
                if (!File.Exists(fontPath))
                {
                    fontPath = @"C:\Windows\Fonts\gulim.ttc,0"; // 대체 폰트
                }

                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                Font koreanFont = new Font(baseFont, 12);
                Font titleFont = new Font(baseFont, 18, Font.BOLD);

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var document = new Document())
                    {
                        PdfWriter writer = PdfWriter.GetInstance(document, fs);
                        document.Open();

                        // 제목 추가
                        document.Add(new Paragraph($"뉴스 동향 보고서: {keyword}", titleFont));
                        document.Add(new Paragraph(" ")); // 빈 줄 추가

                        // 보고서 내용 추가
                        foreach (string line in reportContent.Split('\n'))
                        {
                            document.Add(new Paragraph(line.Trim(), koreanFont));
                        }

                        // 생성 시간 추가
                        document.Add(new Paragraph(" "));
                        document.Add(new Paragraph($"생성 시간: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}", new Font(baseFont, 10, Font.ITALIC)));

                        document.Close();
                    }
                }
                return filePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating PDF report: {ex.Message}", ex);
            }
        }


        private async void GenerateReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateUI("Starting report generation process...");
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var excelFiles = Directory.GetFiles(documentsPath, "*_news_*.xlsx")
                                          .OrderByDescending(f => new FileInfo(f).CreationTime)
                                          .ToList();

                if (!excelFiles.Any())
                {
                    ChatOutputTextBox.Text += "No Excel file found to generate report.\n";
                    return;
                }

                string latestExcelFile = excelFiles.First();
                string keyword = Path.GetFileNameWithoutExtension(latestExcelFile).Split('_')[0]; // 키워드 추출

                string report = await GenerateReportFromExcel(latestExcelFile);
                if (string.IsNullOrWhiteSpace(report))
                {
                    throw new Exception("Generated report is empty.");
                }

                ChatOutputTextBox.Text += "Generated Report:\n" + report + "\n";

                // PDF 생성
                string pdfPath = GeneratePdfReport(report, keyword);

                // 보고서를 이메일로 발송
                string subject = $"뉴스 동향 보고서: {keyword}";
                string body = $"첨부된 PDF 파일에 {keyword}에 대한 뉴스 동향 보고서가 있습니다.";
                string emailResult = await SendEmail(subject, body, pdfPath);
                ChatOutputTextBox.Text += emailResult + "\n";
            }
            catch (Exception ex)
            {
                UpdateUI($"Error generating or sending report: {ex.Message}");
                if (ex.InnerException != null)
                {
                    UpdateUI($"Inner Exception: {ex.InnerException.Message}");
                }
                UpdateUI($"Stack Trace: {ex.StackTrace}");
            }
        }

        private async Task<string> GenerateReportFromExcel(string excelFile)
        {
            var newsData = ReadExcelFile(excelFile);
            string prompt = $@"다음은 스크래핑한 뉴스 데이터입니다:

{newsData}

이 데이터를 바탕으로 다음 지침에 따라 500-700자 내외의 뉴스 동향 보고서를 작성해주세요:

1. 주요 이슈나 사건을 3-4개 정도 선별하여 요약하세요.
2. 각 이슈의 배경과 중요성을 간략히 설명하세요.
3. 이슈들 간의 연관성이 있다면 언급해주세요.
4. 전반적인 뉴스 동향에 대한 분석을 제공하세요.
5. 가능하다면 향후 전망에 대해 간단히 언급해주세요.

보고서는 객관적이고 전문적인 톤으로 작성해주세요.";

            string reportContent = await CallOpenAIAPI(prompt);
            return reportContent;
        }

        private string ReadExcelFile(string filePath)
        {
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension.Rows;

                var sb = new StringBuilder();
                for (int row = 2; row <= rowCount; row++) // 첫 번째 행은 헤더로 가정
                {
                    string title = worksheet.Cells[row, 1].Value?.ToString();
                    string link = worksheet.Cells[row, 2].Value?.ToString();
                    string pubDate = worksheet.Cells[row, 3].Value?.ToString();

                    sb.AppendLine($"제목: {title}");
                    sb.AppendLine($"링크: {link}");
                    sb.AppendLine($"발행일: {pubDate}");
                    sb.AppendLine();
                }

                return sb.ToString();
            }
        }
        private async Task<string> CallOpenAIAPI(string prompt)
        {
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
    {
            new { role = "system", content = "You are a professional news analyst creating detailed reports in Korean. Focus on key events, their implications, and provide a balanced analysis." },
            new { role = "user", content = prompt }
        },
                max_tokens = 1000,
                temperature = 0.7
            };
        

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseBody);

                string generatedContent = jsonResponse["choices"][0]["message"]["content"].ToString();
                if (string.IsNullOrWhiteSpace(generatedContent))
                {
                    throw new Exception("Generated content is empty.");
                }
                return generatedContent;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error calling OpenAI API: {ex.Message}");
            }
        }

        private async void SendChatButton_Click(object sender, RoutedEventArgs e)
        {
            string userInput = ChatInputTextBox.Text;
            if (string.IsNullOrWhiteSpace(userInput)) return;

            UpdateUI($"You: {userInput}");
            ChatInputTextBox.Clear();

            try
            {
                UpdateUI("Waiting for ChatGPT response...");
                string response = await GetChatbotResponseAsync(userInput);
                UpdateUI($"ChatGPT: {response}");
            }
            catch (Exception ex)
            {
                UpdateUI($"Error: {ex.Message}");
            }
        }
        private async void ScrapeNewsButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = NewsKeywordTextBox.Text;
            if (string.IsNullOrWhiteSpace(keyword)) return;

            try
            {
                UpdateUI($"Scraping news for keyword: {keyword}");

                dynamic scrapeNewsFunc = scope.GetVariable("execute_scrape_news");
                var newsItems = await Task.Run(() => scrapeNewsFunc(keyword));

                var newsItemsList = new List<Dictionary<string, string>>();
                foreach (dynamic item in newsItems)
                {
                    newsItemsList.Add(new Dictionary<string, string>
                    {
                        ["title"] = Convert.ToString(item["title"]),
                        ["link"] = Convert.ToString(item["link"]),
                        ["pubDate"] = Convert.ToString(item["pubDate"])
                    });
                }

                string excelFile = await SaveNewsToExcel(newsItemsList, keyword);
                UpdateUI($"News scraped and saved to Excel: {excelFile}");
            }
            catch (Exception ex)
            {
                UpdateUI($"Error scraping news: {ex.Message}");
                UpdateUI($"Stack Trace: {ex.StackTrace}");
            }
        }
        private async Task<string> SaveNewsToExcel(IEnumerable<dynamic> newsItems, string keyword)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string fileName = $"{keyword}_news_{timestamp}.xlsx";
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            await Task.Run(() =>
            {
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("News");
                    worksheet.Cells[1, 1].Value = "Title";
                    worksheet.Cells[1, 2].Value = "Link";
                    worksheet.Cells[1, 3].Value = "Publication Date";

                    int row = 2;
                    foreach (var item in newsItems)
                    {
                        worksheet.Cells[row, 1].Value = item["title"];
                        worksheet.Cells[row, 2].Value = item["link"];
                        worksheet.Cells[row, 3].Value = item["pubDate"];
                        row++;
                    }

                    worksheet.Cells.AutoFitColumns();
                    package.SaveAs(new FileInfo(filePath));
                }
            });
            return filePath;
        }
        private async void SendEmailButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateUI("Preparing to send email...");
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var excelFiles = Directory.GetFiles(documentsPath, "*_news_*.xlsx")
                                          .OrderByDescending(f => new FileInfo(f).CreationTime)
                                          .ToList();

                if (!excelFiles.Any())
                {
                    UpdateUI("No Excel file found to send.");
                    return;
                }

                string latestExcelFile = excelFiles.First();
                string subject = "네이버 뉴스 스크래핑 결과";
                string body = "첨부된 Excel 파일에 스크래핑된 네이버 뉴스 기사가 있습니다.";
                string response = await SendEmail(subject, body, latestExcelFile);
                UpdateUI($"Email sent: {response}");
            }
            catch (Exception ex)
            {
                UpdateUI($"Error sending email: {ex.Message}");
            }
        }

        private async Task<string> SendEmail(string subject, string body, string attachmentPath = null)
        {
            string senderEmail = SenderEmailTextBox.Text;
            string senderPassword = SenderPasswordBox.Password;
            string recipientEmail = RecipientEmailTextBox.Text;

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword) || string.IsNullOrWhiteSpace(recipientEmail))
            {
                return "이메일 설정이 완료되지 않았습니다. 모든 필드를 채워주세요.";
            }

            try
            {
                using (SmtpClient smtpClient = new SmtpClient("smtp.naver.com", 587))
                {
                    smtpClient.EnableSsl = true;
                    smtpClient.Credentials = new NetworkCredential(senderEmail, senderPassword);

                    using (MailMessage mailMessage = new MailMessage(senderEmail, recipientEmail, subject, body))
                    {
                        if (!string.IsNullOrEmpty(attachmentPath))
                        {
                            Attachment attachment = new Attachment(attachmentPath);
                            attachment.Name = Path.GetFileName(attachmentPath); // 파일명 설정

                            // PDF 파일인 경우 MIME 타입 설정
                            if (Path.GetExtension(attachmentPath).ToLower() == ".pdf")
                            {
                                attachment.ContentType = new System.Net.Mime.ContentType("application/pdf");
                            }

                            mailMessage.Attachments.Add(attachment);
                        }

                        await smtpClient.SendMailAsync(mailMessage);
                    }
                }
                return "이메일이 성공적으로 전송되었습니다!";
            }
            catch (Exception ex)
            {
                return $"이메일 전송 중 오류 발생: {ex.Message}";
            }
        }
    }
}
