# 사무 업무 자동화 툴 (News Scraper · Report · Chatbot)

뉴스 수집 → 엑셀 저장 → AI 보고서(PDF) 작성 → 이메일 발송까지 반복적인 사무 업무를 한 화면에서 처리하는 Windows 데스크톱 앱입니다.
C# WPF로 UI와 업무 로직을 만들고, 뉴스 스크래핑은 IronPython으로 Python 스크립트를 호출해 처리합니다.

## 주요 기능

| 기능 | 설명 |
| --- | --- |
| 챗봇 | OpenAI Chat Completions API(`gpt-3.5-turbo`)와 대화하는 간단한 채팅 UI |
| 뉴스 스크래핑 | 키워드를 입력하면 `chatbot.py`의 `execute_scrape_news`로 뉴스(제목/링크/발행일)를 수집 |
| 엑셀 저장 | 수집 결과를 `{키워드}_news_{timestamp}.xlsx`로 내 문서 폴더에 저장 (EPPlus) |
| 보고서 작성 | 가장 최근 엑셀을 읽어 GPT로 500~700자 뉴스 동향 보고서를 생성하고 PDF로 저장 (iTextSharp, 맑은 고딕 임베딩) |
| 이메일 발송 | 엑셀 또는 PDF 보고서를 첨부해 네이버 SMTP로 발송 |

## 동작 흐름

```
키워드 입력 → [Scrape News] → 엑셀 저장
                                  ↓
              [Generate Report] → GPT 요약 → PDF 생성 → 이메일 발송
              [Send Email with News] → 엑셀 첨부 발송
```

## 기술 스택

- **언어/프레임워크**: C#, WPF, .NET Framework 4.7.2
- **스크립트 연동**: IronPython 3.4.1 (뉴스 스크래핑 Python 함수 호출)
- **AI**: OpenAI Chat Completions API (REST, `HttpClient` + Newtonsoft.Json)
- **문서 처리**: EPPlus 7.4 (Excel), iTextSharp 5.5 (PDF)
- **메일**: `System.Net.Mail.SmtpClient` (smtp.naver.com:587, SSL)

## 프로젝트 구조

```
├── App.xaml / App.xaml.cs        # 앱 진입점
├── MainWindow.xaml               # UI (챗봇, 뉴스 키워드, 메일 설정, 버튼)
├── MainWindow.xaml.cs            # 챗봇 / 스크래핑 / 엑셀 / 보고서 / 메일 로직
├── chatbot.py                    # IronPython에서 호출하는 스크래핑 함수
├── packages.config               # NuGet 패키지 목록
└── wpf_ironpython.sln            # 솔루션 파일
```

## 실행 방법

1. Visual Studio에서 `wpf_ironpython.sln`을 열고 NuGet 패키지를 복원합니다.
2. OpenAI API 키를 **환경변수**로 설정합니다.
   ```
   setx OPENAI_API_KEY "여기에_본인_키"
   ```
3. `chatbot.py`가 실행 파일(`bin/Debug`)과 같은 폴더에 복사되도록 빌드 설정을 확인합니다. (속성 → 출력 디렉터리로 복사)
4. 빌드 후 실행합니다.
5. 메일 발송을 쓰려면 앱 하단 **Email Settings**에 네이버 메일 주소 / 비밀번호(앱 비밀번호 권장) / 수신자를 입력합니다.

## 사용 방법

1. **Scrape News**: 키워드를 입력하고 버튼 클릭 → 문서 폴더에 엑셀 생성
2. **Generate Report**: 최신 엑셀 기반으로 보고서 생성 → PDF 저장 → 설정된 수신자에게 메일 발송
3. **Send Email with News**: 최신 엑셀을 그대로 메일로 발송
4. **Chatbot**: 입력창에 질문을 입력하고 Send

## 개선 예정

- API 키/메일 계정 등 민감 정보를 설정 파일·환경변수로 분리
- MVVM 구조로 `MainWindow.xaml.cs` 로직 분리
- 보고서 프롬프트/모델 선택 옵션화
- 스크래핑 대상 사이트 확장 및 예외 처리 강화

## 개발자

심재성 ([@js-git-del](https://github.com/js-git-del))
