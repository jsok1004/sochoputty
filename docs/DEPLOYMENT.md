# Socho Terminal Manager 배포 가이드

이 문서는 **Socho Terminal Manager**(.NET 9.0 WPF Windows 데스크톱 애플리케이션)를 최종 사용자에게 전달하기 위한 배포 방식과 절차를 설명합니다.

> 이 앱은 임베딩된 **PowerShell 콘솔에서 Windows 기본 OpenSSH(`ssh`)** 를 실행합니다. 더 이상 PuTTY를 번들하지 않습니다.

---

## 1. 대상 프로그램 개요

| 항목 | 내용 |
|------|------|
| 애플리케이션 | Socho Terminal Manager |
| 유형 | Windows 데스크톱 (WPF + WinForms 임베딩) |
| 대상 프레임워크 | `net9.0-windows` |
| 출력 형식 | `WinExe` (`SochoPutty.exe`) |
| 아키텍처 | 주로 `win-x64` (x86/ARM64 필요 시 별도 빌드) |
| 런타임 의존성 | Windows 기본 **OpenSSH 클라이언트**(`ssh.exe`), **PowerShell**(`powershell.exe`), **conhost.exe** — 모두 Windows 기본 제공 |
| 사용자 데이터 위치 | `%APPDATA%\SochoPutty\` (`connections.json`, `settings.json`) |

> ✅ **별도 번들이 필요 없습니다.** 접속에 사용하는 `ssh`/`powershell`/`conhost`는 모두 Windows에 기본 포함되어 있으므로, 배포 산출물에 추가로 동봉할 외부 실행 파일이 없습니다.

---

## 2. 배포 방식 선택

| 방식 | 사용자 요구사항 | 산출물 크기 | 권장 상황 |
|------|----------------|------------|----------|
| **A. 프레임워크 종속 (Framework-dependent)** | .NET 9 Desktop Runtime 설치 필요 | 작음 (~수 MB) | 사내 배포처럼 런타임 설치를 통제할 수 있는 환경 |
| **B. 자체 포함 단일 파일 (Self-contained single-file)** ✅권장 | **없음** (런타임 포함) | 큼 (~150MB+) | 일반 사용자 배포. "다운로드 후 바로 실행" |

> **권장:** 일반 배포는 **방식 B(자체 포함 단일 실행 파일)** 를 사용합니다. 최종 사용자가 .NET 런타임을 따로 설치하지 않아도 됩니다.
>
> ⚠️ 두 방식 모두 **대상 PC에 Windows OpenSSH 클라이언트가 설치되어 있어야** 접속이 동작합니다(아래 11장 참고). Windows 10 1809 이상은 기본 포함이지만, 조직 정책으로 제거된 환경일 수 있으니 확인이 필요합니다.

---

## 3. 사전 준비

배포 빌드를 만드는 PC(빌드 머신)에 다음이 필요합니다.

1. **.NET 9 SDK** — <https://dotnet.microsoft.com/download/dotnet/9.0>
   ```powershell
   dotnet --version   # 9.x 확인
   ```
2. (선택) 코드 서명 인증서 — SmartScreen 경고 완화용 (아래 8장 참고)

> 빌드 머신에는 외부 SSH/PuTTY 바이너리를 준비할 필요가 없습니다.

---

## 4. 방식 A — 프레임워크 종속 배포

```powershell
dotnet publish SochoPutty.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish\fdd
```

- 산출물: `publish\fdd\` 폴더 전체 (`SochoPutty.exe`, DLL)
- **사용자 요구사항:** [.NET 9.0 **Desktop** Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) 설치 + Windows OpenSSH 클라이언트
- 배포: 폴더 전체를 압축(zip)하여 전달하거나 설치 프로그램(7장)으로 패키징

---

## 5. 방식 B — 자체 포함 단일 파일 배포 (권장)

```powershell
dotnet publish SochoPutty.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish\single
```

### 옵션 설명
| 옵션 | 의미 |
|------|------|
| `--self-contained true` | .NET 런타임을 산출물에 포함 |
| `PublishSingleFile=true` | 관리 DLL을 하나의 `.exe`로 묶음 |
| `IncludeNativeLibrariesForSelfExtract=true` | 네이티브 라이브러리까지 단일 파일에 포함 (WPF 필요) |

### 결과물
```
publish\single\
└── SochoPutty.exe      ← 사용자에게 전달하는 단일 실행 파일
```

> 외부 SSH 바이너리를 동봉할 필요가 없어, 단일 `SochoPutty.exe` 하나만 전달하면 됩니다.

### 선택 최적화 옵션
- 크기 축소(트리밍): WPF는 트리밍 호환성 이슈가 있어 **`PublishTrimmed`는 권장하지 않습니다.**
- 시작 속도 향상: `-p:PublishReadyToRun=true` 추가 가능(산출물 크기 다소 증가).

---

## 6. 버전 관리

- **소스 위치:** `SochoPutty.csproj`의 `<AssemblyVersion>` / `<FileVersion>` (현재 `1.5.0`)
- **Git 태그와 동기화:** 릴리스마다 태그를 만듭니다.
  ```powershell
  git tag v1.5.0
  git push origin v1.5.0
  ```

명령줄에서 일회성으로 재정의하려면:
```powershell
dotnet publish ... -p:Version=1.5.0
```

---

## 7. 설치 프로그램 패키징 (선택)

### 7.1 Inno Setup (권장 · 간단)
1. [Inno Setup](https://jrsoftware.org/isinfo.php) 설치
2. `publish\single` 폴더 내용을 설치 대상으로 지정하는 스크립트(`setup.iss`) 작성 — 핵심 항목:
   - `SochoPutty.exe` 를 설치 폴더에 복사
   - 시작 메뉴 / 바탕화면 바로가기 생성 (아이콘: `app.ico`)
   - 제거(uninstall) 시 프로그램 파일만 제거하고 **`%APPDATA%\SochoPutty` 사용자 데이터는 보존** 권장
   - (선택) 설치 마법사에서 OpenSSH 클라이언트 존재 여부를 점검하는 안내 추가
3. 컴파일 → `SochoTerminalSetup_1.5.0.exe` 생성

### 7.2 MSIX
Microsoft Store 또는 엔터프라이즈 배포가 필요하면 MSIX 패키징 프로젝트를 추가할 수 있습니다. 코드 서명이 사실상 필수입니다.

---

## 8. 코드 서명 (권장)

서명하지 않은 실행 파일은 Windows **SmartScreen** 경고가 표시됩니다.

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  /f mycert.pfx /p <password> publish\single\SochoPutty.exe
```

- 설치 프로그램도 동일하게 서명합니다.
- 인증서가 없으면 서명은 생략 가능하나, 사용자에게 "추가 정보 → 실행" 안내가 필요합니다.

---

## 9. 배포 산출물 체크리스트

- [ ] `.csproj` 버전과 Git 태그가 일치하는가
- [ ] 깨끗한(런타임 미설치) Windows 10/11 x64 환경에서 실행 테스트 완료했는가 (방식 B)
- [ ] **대상 환경에 OpenSSH 클라이언트가 설치**되어 있는가 (`ssh -V` 확인)
- [ ] 새 연결 생성 → **PowerShell 콘솔 임베딩** → `ssh` 접속 → `%APPDATA%\SochoPutty\connections.json` 저장 동작 확인
- [ ] 키 인증(`-i`) 및 비밀번호 대화형 입력 동작 확인
- [ ] (선택) 코드 서명 완료 및 SmartScreen 경고 확인
- [ ] 릴리스 노트 / 버전 히스토리(`README.md`) 업데이트

---

## 10. 릴리스 배포 (GitHub Release 예시)

1. 태그 푸시 후 GitHub에서 Release 초안 작성
2. 첨부물 업로드:
   - `SochoTerminal-1.5.0-win-x64.zip` (방식 B 산출물)
   - 또는 `SochoTerminalSetup_1.5.0.exe` (설치 프로그램)
3. 릴리스 노트에 변경 사항, 시스템 요구사항(Windows 10 1809 이상 + OpenSSH 클라이언트), 데이터 저장 위치 기재

---

## 11. 시스템 요구사항 (사용자 안내용)

| 항목 | 요구사항 |
|------|----------|
| 운영체제 | Windows 10 (1809, x64) 이상 |
| .NET 런타임 | 방식 A: .NET 9 Desktop Runtime 필요 / 방식 B: 불필요(포함) |
| OpenSSH 클라이언트 | **필요** — 설정 > 앱 > 선택적 기능 > "OpenSSH 클라이언트" (Windows 기본 포함) |
| PowerShell | Windows 기본 포함 |
| 권한 | `%APPDATA%` 쓰기 권한 (설정·연결 정보 저장용) |

---

## 부록. 자주 발생하는 배포 문제

| 증상 | 원인 | 해결 |
|------|------|------|
| "OpenSSH 클라이언트(ssh.exe)를 찾을 수 없음" | 대상 PC에 OpenSSH 클라이언트 미설치 | 설정 > 앱 > 선택적 기능에서 "OpenSSH 클라이언트" 설치 |
| 접속은 열리나 비밀번호 자동 입력이 안 됨 | OpenSSH는 비밀번호 명령줄 전송 불가(사양) | 터미널에 직접 입력하거나 개인키(`-i`) 인증 사용 |
| "이 앱이 PC에서 실행되지 않습니다" | 아키텍처 불일치 (x86 vs x64) | 대상에 맞는 `-r` 로 재빌드 |
| WPF 관련 DLL 로드 오류 (방식 A) | .NET **Desktop** Runtime 미설치 | Desktop Runtime 설치 |
| 콘솔이 탭 안에 임베딩되지 않고 별도 창으로 뜸 | (드물게) conhost 강제 실패 | 로그 확인. `conhost.exe` 경유 실행이 정상 동작하는지 점검 |
| SmartScreen 경고 | 미서명 실행 파일 | 코드 서명(8장) 또는 "추가 정보 → 실행" 안내 |
| 설정이 저장되지 않음 | `%APPDATA%` 쓰기 권한/백신 차단 | 권한 및 실시간 보호 예외 확인 |
