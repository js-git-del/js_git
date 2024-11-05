import sys
import clr
clr.AddReference("System.Net")
from System.Net import WebClient
from System.Text import Encoding
import re


def scrape_naver_news(keyword):
    url = f"https://search.naver.com/search.naver?where=news&query={keyword}"
    client = WebClient()
    client.Encoding = Encoding.UTF8
    html = client.DownloadString(url)
    
    news_items = []
    
    # 간단한 정규표현식을 사용하여 뉴스 항목 추출
    pattern = r'<a href="(https?://[^"]+)" class="news_tit" .*?title="([^"]+)".*?<span class="info">([^<]+)</span>'
    matches = re.findall(pattern, html)
    for match in matches[:10]:
        link, title, pubDate = match
        news_item = {
            'title': title,
            'link': link,
            'pubDate': pubDate.strip()
        }
        news_items.append(news_item)
        print(f"News item: {news_item}")  # 디버깅을 위한 출력
    
    print(f"Total news items: {len(news_items)}")  # 총 뉴스 항목 수 출력
    return news_items

def send_email_with_excel(excel_file):
    sender_email = "tlawotjd1234@naver.com"
    sender_password = "10fiTek!@#~"
    recipient_email = "tlawotjd1234@naver.com"
    subject = "네이버 뉴스 스크래핑 결과"
    body = "첨부된 Excel 파일에 스크래핑된 네이버 뉴스 기사가 있습니다."
    
    try:
        smtp_client = SmtpClient("smtp.naver.com", 587)
        smtp_client.EnableSsl = True
        smtp_client.Credentials = NetworkCredential(sender_email, sender_password)
        
        message = MailMessage(sender_email, recipient_email, subject, body)
        message.BodyEncoding = Encoding.UTF8
        message.SubjectEncoding = Encoding.UTF8
        
        attachment = Attachment(excel_file)
        message.Attachments.Add(attachment)
        
        smtp_client.Send(message)
        return "이메일이 성공적으로 전송되었습니다!"
    except Exception as e:
        return f"이메일 전송 중 오류 발생: {str(e)}"

# 이 함수들은 C#에서 호출됩니다
def execute_scrape_news(keyword):
    return scrape_naver_news(keyword)

def execute_send_email_with_excel(excel_file):
    return send_email_with_excel(excel_file)