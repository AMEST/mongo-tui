# Требования и установка

[English](requirements.md) | **Русский** · [К README](../README_RU.md)

[macOS](#macos) · [Linux](#linux) · [Windows](#windows) ·
[Сборка из исходников](#сборка-из-исходников) · [Публикация сборки](#публикация-сборки)

## Что нужно установить

Для MongoTui нужен интерактивный терминал и доступ к локальному или удалённому серверу MongoDB.
Инструменты устанавливаются на машину, **где запускается MongoTui**. Локальный сервер MongoDB необязателен.

| Компонент | Когда нужен |
|---|---|
| MongoDB Shell (`mongosh`) | Выполнение запросов во вкладках. Дерево подключений и операции с документами используют встроенный драйвер. |
| MongoDB Database Tools (`mongoimport`, `mongoexport`) | Импорт и экспорт коллекций. Устанавливаются отдельно от `mongosh`. |
| Клиент OpenSSH 8.4+ (`ssh`) | Подключения через SSH/TPAM, включая интерактивную 2FA. |
| .NET Runtime 10 | Только для framework-dependent сборок. Релизные архивы и self-contained сборки уже содержат runtime. |
| .NET SDK 10 и Git | Сборка из исходников. Runtime уже входит в SDK. |
| Docker | Только для интеграционных тестов. |

Внешние инструменты должны быть в `PATH`, с которым запускается MongoTui. Для самостоятельных
пакетов MongoDB Node.js/npm не нужны. Архив MongoTui нужно распаковывать целиком: нативные
библиотеки SQLite и Oniguruma должны оставаться рядом с исполняемым файлом.

## macOS

### Инструменты MongoDB и SSH

Если установлен [Homebrew](https://brew.sh/):

```sh
brew tap mongodb/brew
brew install mongosh
brew install mongodb-database-tools
ssh -V
```

Пакеты доступны через [MongoDB Homebrew tap](https://github.com/mongodb/homebrew-brew).
Database Tools можно пропустить, если импорт и экспорт не нужны. Для SSH/TPAM проверьте,
что `ssh -V` показывает версию не ниже 8.4. При необходимости установите новый клиент и выберите его для текущего терминала:

```sh
brew install openssh
export PATH="$(brew --prefix openssh)/bin:$PATH"
```

Добавьте этот `export` в `~/.zshrc`, чтобы настройка действовала и в новых терминалах.
Без Homebrew скачайте ZIP-архивы [Shell](https://www.mongodb.com/try/download/shell) и
[Database Tools](https://www.mongodb.com/try/download/database-tools) для своей архитектуры,
распакуйте их и добавьте каталоги `bin` в `PATH`.

### .NET для других типов сборок

Для релизного архива этот шаг не нужен. Для остальных сборок откройте
[страницу .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0),
выберите **macOS → Arm64** для Apple Silicon или **x64** для Intel и запустите установщик `.pkg`:

| Тип сборки | Что установить |
|---|---|
| Framework-dependent | .NET Runtime 10 |
| Сборка/запуск из исходников | .NET SDK 10 |

Для исходников также установите Git: `brew install git`. Поддерживаемые версии ОС и пути
установки описаны в [инструкции .NET для macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos).

### Запуск релиза

Скачайте `mongotui-osx-arm64.tar.gz` для Apple Silicon или `mongotui-osx-x64.tar.gz` для Intel
из [Releases](https://github.com/AMEST/mongo-tui/releases). Пример:

```sh
mkdir -p ./mongotui
tar -xzf ./mongotui-osx-arm64.tar.gz -C ./mongotui
cd ./mongotui
xattr -d com.apple.quarantine ./mongo-tui
xattr -d com.apple.quarantine ./libe_sqlite3.dylib
xattr -d com.apple.quarantine ./libonigwrap.dylib
chmod +x ./mongo-tui
./mongo-tui
```

**Скачанные бинарники не подписаны и не нотаризованы. Перед первым запуском обязательно
снимите карантин, чтобы пройти Gatekeeper.** Сообщение `No such xattr` означает, что у файла
уже нет атрибута карантина; продолжайте выполнять остальные команды.

## Linux

### Инструменты MongoDB и SSH

Скачайте [MongoDB Shell](https://www.mongodb.com/try/download/shell) и, при необходимости,
[Database Tools](https://www.mongodb.com/try/download/database-tools). Выберите свой
дистрибутив и архитектуру процессора, тип пакета — `deb` или `rpm`.
Для Shell выбирайте обычный пакет, если вам специально не нужна сборка с shared OpenSSL.

Выполняйте команды в каталоге со скачанными пакетами. Оставьте в нём только одну версию
каждого пакета; уберите аргумент Database Tools, если скачали только Shell.
Этот способ не требует предварительного подключения репозитория MongoDB.

Debian / Ubuntu:

```sh
sudo apt-get update
sudo apt install ./mongodb-mongosh_*.deb ./mongodb-database-tools-*.deb
sudo apt-get install -y openssh-client
```

RHEL / совместимые дистрибутивы с `dnf`:

```sh
sudo dnf install ./mongodb-mongosh-*.rpm ./mongodb-database-tools-*.rpm
sudo dnf install -y openssh-clients
```

Проверьте `ssh -V`: для SSH/TPAM нужна версия 8.4+. У MongoDB есть инструкции установки пакетов
[DEB](https://www.mongodb.com/docs/database-tools/installation/?operating-system=linux&package-type=deb)
и [RPM](https://www.mongodb.com/docs/database-tools/installation/?operating-system=linux&package-type=rpm).

### Системные библиотеки и .NET

Даже self-contained сборке нужны системные библиотеки .NET. Пример для **Ubuntu 24.04**:

```sh
sudo apt-get update
sudo apt-get install -y ca-certificates libc6 libgcc-s1 libgssapi-krb5-2 libicu74 libssl3t64 libstdc++6 tzdata zlib1g
```

Для framework-dependent сборки на **Ubuntu 24.04** установите runtime:

```sh
sudo apt-get install -y dotnet-runtime-10.0
```

Для сборки из исходников вместо него установите SDK и Git:

```sh
sudo apt-get install -y dotnet-sdk-10.0 git
```

Эти пакеты .NET доступны в собственных репозиториях Ubuntu 24.04. Для других систем названия
пакетов и настройка отличаются: прямые инструкции для
[версий Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install),
[Debian](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian) и
[других дистрибутивов](https://learn.microsoft.com/en-us/dotnet/core/install/linux).
Список системных библиотек выше относится именно к Ubuntu 24.04.

### Запуск релиза

Выберите `mongotui-linux-x64.tar.gz` для x86-64 или `mongotui-linux-arm64.tar.gz` для ARM64
из [Releases](https://github.com/AMEST/mongo-tui/releases). Пример:

```sh
mkdir -p ./mongotui
tar -xzf ./mongotui-linux-x64.tar.gz -C ./mongotui
cd ./mongotui
chmod +x ./mongo-tui
./mongo-tui
```

## Windows

### Инструменты MongoDB и PATH

1. Скачайте **Windows x64 ZIP** со страницы [MongoDB Shell](https://www.mongodb.com/try/download/shell).
   Распакуйте его так, чтобы `mongosh.exe` находился по пути `C:\Tools\mongosh\bin\mongosh.exe`.
2. Для импорта и экспорта скачайте **Windows x86_64 ZIP** со страницы
   [Database Tools](https://www.mongodb.com/try/download/database-tools). Распакуйте его так,
   чтобы `mongoimport.exe` и `mongoexport.exe` находились в `C:\Tools\mongodb-database-tools\bin`.
3. Через поиск Windows откройте **Изменение переменных среды текущего пользователя → Path → Изменить → Создать**.
   Добавьте `C:\Tools\mongosh\bin` и, если установлены Database Tools, `C:\Tools\mongodb-database-tools\bin`.
   Сохраните изменения и откройте новый терминал.

Можно выбрать другие каталоги; в `PATH` добавляются фактические папки с исполняемыми файлами.
Для проверки этих путей в текущем сеансе PowerShell:

```powershell
$env:Path = "C:\Tools\mongosh\bin;C:\Tools\mongodb-database-tools\bin;$env:Path"
```

### SSH/TPAM

Проверьте `ssh -V` в PowerShell. Если клиента нет, установите дополнительный компонент Windows
из **PowerShell с правами администратора**:

```powershell
Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0
```

Версия встроенного клиента Windows может быть ниже 8.4. В таком случае скачайте `OpenSSH-Win64.zip`
из [официальных релизов Win32-OpenSSH](https://github.com/PowerShell/Win32-OpenSSH/releases),
распакуйте содержимое в `C:\Tools\OpenSSH` и выберите его в терминале, из которого запускаете MongoTui:

```powershell
$env:Path = "C:\Tools\OpenSSH;$env:Path"
ssh -V
```

Эта папка должна стоять в `PATH` раньше старых SSH-клиентов; устанавливать SSH-сервер не нужно.
См. [инструкцию Microsoft по установке клиента OpenSSH](https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse).

### .NET для других типов сборок

Для релизного архива этот шаг не нужен. Для framework-dependent сборки выполните в PowerShell:

```powershell
winget install --id Microsoft.DotNet.Runtime.10 --exact
```

Для сборки из исходников вместо runtime установите SDK и Git:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
winget install --id Git.Git --exact --source winget
```

После установки откройте терминал заново. Если WinGet недоступен, используйте
[установщики .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) и
[Git для Windows](https://git-scm.com/install/windows).
Идентификаторы пакетов приведены в [инструкции Microsoft для .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

### Запуск релиза

Скачайте `mongotui-windows-x64.tar.gz` из [Releases](https://github.com/AMEST/mongo-tui/releases).
Распакуйте архив своим архиватором или через PowerShell из каталога загрузки:

```powershell
New-Item -ItemType Directory -Path .\mongotui
tar -xzf .\mongotui-windows-x64.tar.gz -C .\mongotui
cd .\mongotui
.\mongo-tui.exe
```

## Проверка зависимостей

Проверьте нужные инструменты в том терминале, из которого запускаете MongoTui:

```sh
mongosh --version
mongoimport --version
mongoexport --version
ssh -V
```

Для framework-dependent сборки в выводе `dotnet --list-runtimes` должен быть
`Microsoft.NETCore.App 10.0.x`. Для исходников в `dotnet --list-sdks` должна быть версия `10.0.x`.
Если инструмент работает в другом терминале, но MongoTui его не находит, перезапустите
MongoTui из терминала с обновлённым `PATH`.

## Сборка из исходников

Установите SDK и Git по инструкции для своей ОС выше, а также внешние инструменты для
нужных функций. В терминале или PowerShell:

```sh
git clone https://github.com/AMEST/mongo-tui.git
cd mongo-tui
dotnet run --project src/MongoTui.TUI
```

Сборка и тесты без Docker:

```sh
dotnet build MongoTui.sln
dotnet test MongoTui.sln --filter "Category!=Integration"
```

Для полного набора тестов сначала установите и запустите Docker:

| ОС | Установка |
|---|---|
| macOS | `brew install --cask docker-desktop`, затем откройте Docker Desktop. |
| Linux | Инструкция [Docker Engine для Ubuntu](https://docs.docker.com/engine/install/ubuntu/) или [своего дистрибутива](https://docs.docker.com/engine/install/). Настройте [доступ без root](https://docs.docker.com/engine/install/linux-postinstall/) для пользователя, запускающего тесты. |
| Windows | Установите и откройте [Docker Desktop с WSL 2](https://docs.docker.com/desktop/setup/install/windows-install/), используйте Linux-контейнеры. |

Когда `docker info` выполняется успешно:

```sh
dotnet test MongoTui.sln
```

## Публикация сборки

Выполняйте команды из корня репозитория с установленным .NET SDK 10. Выберите идентификатор платформы (RID):

| ОС / архитектура | RID |
|---|---|
| macOS Apple Silicon | `osx-arm64` |
| macOS Intel | `osx-x64` |
| Linux x86-64 | `linux-x64` |
| Linux ARM64 | `linux-arm64` |
| Windows x64 | `win-x64` |

Пример self-contained сборки, как в релизах. Замените `linux-x64` в обоих местах на свой RID:

```sh
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
```

Распространяйте **весь каталог результата**. Из него запускайте `./mongo-tui` на macOS/Linux
или `.\mongo-tui.exe` на Windows. Runtime включён; внешние инструменты MongoDB и системные
библиотеки Linux всё равно нужно установить.

Для framework-dependent сборки укажите нужный RID и установите .NET Runtime 10 на целевой машине:

```sh
dotnet publish src/MongoTui.TUI/MongoTui.TUI.csproj -c Release -r linux-x64 --self-contained false -o publish/framework-dependent
dotnet publish/framework-dependent/mongo-tui.dll
```

Этой сборке тоже нужен весь каталог результата. Для скачанных сборок macOS выполните
[инструкцию Gatekeeper](../README_RU.md#macos--обязательно-перед-первым-запуском).
