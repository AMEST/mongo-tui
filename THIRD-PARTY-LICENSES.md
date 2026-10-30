# Third-party licenses

MongoTui's own source code is covered by [LICENSE](LICENSE). Its dependencies retain their respective licenses.

This inventory was checked on 2026-10-21 against `Directory.Packages.props`, the project references and all nine restored `obj/project.assets.json` files for .NET 10. It includes 93 resolved NuGet package/version pairs, both direct and transitive. Package links point to the exact versions on NuGet, including their license metadata and source repositories. Attribution comes from package copyright metadata or upstream license notices; entries marked **Authors** use package author metadata where no copyright notice was available.

The common license texts below apply together with the corresponding attribution notices in the tables. Additional bundled components and upstream notices are listed after the tables.

## Web interface additions (2026-10-23)

The browser editor bundles CodeMirror 6 and its Lezer parsers, all under MIT licenses.
Complete package notices are embedded and served at `/_content/MongoTui.Web/editor-LICENSES.txt`
and retained in [the source asset](src/MongoTui.Web/wwwroot/editor-LICENSES.txt).
The pinned dependency graph and build tool versions are in
[ClientAssets/package-lock.json](src/MongoTui.Web/ClientAssets/package-lock.json).
The .NET host also includes ASP.NET Core and its Blazor browser assets (Microsoft, MIT),
provided by the .NET shared framework and `Microsoft.AspNetCore.App.Internal.Assets`.
Embedded asset serving adds [Microsoft.Extensions.FileProviders.Embedded 10.0.12](https://www.nuget.org/packages/Microsoft.Extensions.FileProviders.Embedded/10.0.12)
(MIT; © Microsoft Corporation. All rights reserved.).
The existing NuGet inventory below predates these web additions.

## Application dependencies

Includes packages used to build the application, such as analyzers, as well as runtime libraries.

| Package | Version | License | Copyright / attribution |
|---|---|---|---|
| [CliWrap](https://www.nuget.org/packages/CliWrap/3.10.2) | 3.10.2 | [MIT](#mit) | Copyright (C) Oleksii Holub |
| [ColorHelper](https://www.nuget.org/packages/ColorHelper/1.8.1) | 1.8.1 | [MIT](#mit) | **Authors:** Artyom Gritsuk |
| [DnsClient](https://www.nuget.org/packages/DnsClient/1.6.1) | 1.6.1 | [Apache-2.0](#apache-20) | Copyright (c) 2021 Michael Conrad |
| [JetBrains.Annotations](https://www.nuget.org/packages/JetBrains.Annotations/2026.2.0) | 2026.2.0 | [MIT](#mit) | Copyright (c) 2016-2025 JetBrains s.r.o. |
| [Konscious.Security.Cryptography.Argon2](https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2/1.3.1) | 1.3.1 | [MIT](#mit) | (c) 2024 Keef Aragon |
| [Konscious.Security.Cryptography.Blake2](https://www.nuget.org/packages/Konscious.Security.Cryptography.Blake2/1.1.1) | 1.1.1 | [MIT](#mit) | (c) 2024 Keef Aragon |
| [Latchkey](https://www.nuget.org/packages/Latchkey/0.1.0) | 0.1.0 | [MIT](#mit) | Copyright (c) Latchkey contributors |
| [linq2db](https://www.nuget.org/packages/linq2db/6.5.0) | 6.5.0 | [MIT](#mit) | Copyright © 2026 Igor Tkachev, Ilya Chudin, Svyatoslav Danyliv, Dmitry Lukashenko, and others |
| [linq2db.Analyzers](https://www.nuget.org/packages/linq2db.Analyzers/6.5.0) | 6.5.0 | [MIT](#mit) | Copyright © 2026 Igor Tkachev, Ilya Chudin, Svyatoslav Danyliv, Dmitry Lukashenko, and others |
| [Markdig](https://www.nuget.org/packages/Markdig/1.3.2) | 1.3.2 | [BSD-2-Clause](#bsd-2-clause) | Copyright (c) 2018-2019, Alexandre Mutel. All rights reserved. |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Data.Sqlite.Core](https://www.nuget.org/packages/Microsoft.Data.Sqlite.Core/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration.Binder](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Binder/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration.CommandLine](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.CommandLine/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration.EnvironmentVariables](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.EnvironmentVariables/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration.FileExtensions](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.FileExtensions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration.Json](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Configuration.UserSecrets](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.UserSecrets/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Diagnostics](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Diagnostics.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics.Abstractions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.FileProviders.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.FileProviders.Abstractions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.FileProviders.Physical](https://www.nuget.org/packages/Microsoft.Extensions.FileProviders.Physical/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.FileSystemGlobbing](https://www.nuget.org/packages/Microsoft.Extensions.FileSystemGlobbing/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Http](https://www.nuget.org/packages/Microsoft.Extensions.Http/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Logging.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Configuration/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Logging.Debug](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Debug/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Logging.EventLog](https://www.nuget.org/packages/Microsoft.Extensions.Logging.EventLog/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Logging.EventSource](https://www.nuget.org/packages/Microsoft.Extensions.Logging.EventSource/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Options](https://www.nuget.org/packages/Microsoft.Extensions.Options/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Options.ConfigurationExtensions](https://www.nuget.org/packages/Microsoft.Extensions.Options.ConfigurationExtensions/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.Extensions.Primitives](https://www.nuget.org/packages/Microsoft.Extensions.Primitives/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [MongoDB.Bson](https://www.nuget.org/packages/MongoDB.Bson/3.12.0) | 3.12.0 | [Apache-2.0](#apache-20) | Copyright © 2010-present MongoDB Inc. |
| [MongoDB.Driver](https://www.nuget.org/packages/MongoDB.Driver/3.12.0) | 3.12.0 | [Apache-2.0](#apache-20) | Copyright © 2010-present MongoDB Inc. |
| [Onigwrap](https://www.nuget.org/packages/Onigwrap/1.0.11) | 1.0.11 | [MIT](#mit) | Copyright (c) 2024 Aikawa Yataro |
| [Serilog](https://www.nuget.org/packages/Serilog/4.4.0) | 4.4.0 | [Apache-2.0](#apache-20) | Copyright © Serilog Contributors |
| [Serilog.Extensions.Logging](https://www.nuget.org/packages/Serilog.Extensions.Logging/10.0.0) | 10.0.0 | [Apache-2.0](#apache-20) | **Authors:** Microsoft,Serilog Contributors |
| [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0) | 7.0.0 | [Apache-2.0](#apache-20) | **Authors:** Serilog Contributors |
| [SharpCompress](https://www.nuget.org/packages/SharpCompress/0.48.1) | 0.48.1 | [MIT](#mit) | Copyright (c) 2025  Adam Hathcock |
| [Snappier](https://www.nuget.org/packages/Snappier/1.3.1) | 1.3.1 | [BSD-3-Clause](#bsd-3-clause) | Copyright 2011-2020, Google, Inc. and Snappier Authors |
| [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.12) | 2.1.12 | [Apache-2.0](#apache-20) | Copyright 2014-2024 SourceGear, LLC |
| [SQLitePCLRaw.core](https://www.nuget.org/packages/SQLitePCLRaw.core/2.1.12) | 2.1.12 | [Apache-2.0](#apache-20) | Copyright 2014-2024 SourceGear, LLC |
| [SQLitePCLRaw.lib.e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.12) | 2.1.12 | [Apache-2.0](#apache-20) | Copyright 2014-2024 SourceGear, LLC |
| [SQLitePCLRaw.provider.e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.provider.e_sqlite3/2.1.12) | 2.1.12 | [Apache-2.0](#apache-20) | Copyright 2014-2024 SourceGear, LLC |
| [System.Diagnostics.EventLog](https://www.nuget.org/packages/System.Diagnostics.EventLog/10.0.12) | 10.0.12 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [System.IO.Abstractions](https://www.nuget.org/packages/System.IO.Abstractions/22.2.0) | 22.2.0 | [MIT](#mit) | Copyright © Tatham Oddie & friends 2010-2026 |
| [Terminal.Gui](https://www.nuget.org/packages/Terminal.Gui/2.5.0) | 2.5.0 | [MIT](#mit) | Copyright 2007-2011 Novell Inc; Copyright 2017 Microsoft Corp |
| [TestableIO.System.IO.Abstractions](https://www.nuget.org/packages/TestableIO.System.IO.Abstractions/22.2.0) | 22.2.0 | [MIT](#mit) | Copyright © Tatham Oddie & friends 2010-2026 |
| [TestableIO.System.IO.Abstractions.Wrappers](https://www.nuget.org/packages/TestableIO.System.IO.Abstractions.Wrappers/22.2.0) | 22.2.0 | [MIT](#mit) | Copyright © Tatham Oddie & friends 2010-2026 |
| [Testably.Abstractions.FileSystem.Interface](https://www.nuget.org/packages/Testably.Abstractions.FileSystem.Interface/10.3.0) | 10.3.0 | [MIT](#mit) | Copyright (c) 2024- 2026 Testably |
| [TextMateSharp](https://www.nuget.org/packages/TextMateSharp/2.0.4) | 2.0.4 | [MIT](#mit) | Copyright (c) 2021 Daniel Peñalba |
| [TextMateSharp.Grammars](https://www.nuget.org/packages/TextMateSharp.Grammars/2.0.4) | 2.0.4 | [MIT](#mit) | Copyright (c) 2021 Daniel Peñalba |
| [Wcwidth](https://www.nuget.org/packages/Wcwidth/4.0.1) | 4.0.1 | [MIT](#mit) | Copyright Patrik Svensson. Phil Scott |
| [ZstdSharp.Port](https://www.nuget.org/packages/ZstdSharp.Port/0.7.3) | 0.7.3 | [MIT](#mit) | Copyright Oleg Stepanischev 2023 |

## Test and development dependencies

These package/version pairs occur only in test projects' dependency graphs. Application dependencies also used by tests are listed above.

| Package | Version | License | Copyright / attribution |
|---|---|---|---|
| [AwesomeAssertions](https://www.nuget.org/packages/AwesomeAssertions/9.6.0) | 9.6.0 | [Apache-2.0](#apache-20) | Copyright Dennis Doomen 2010-2025, AwesomeAssertions 2025- |
| [BouncyCastle.Cryptography](https://www.nuget.org/packages/BouncyCastle.Cryptography/2.7.0) | 2.7.0 | [MIT](#mit) | Copyright (c) 2000-2026 The Legion of the Bouncy Castle Inc. (https://www.bouncycastle.org). |
| [Castle.Core](https://www.nuget.org/packages/Castle.Core/5.1.1) | 5.1.1 | [Apache-2.0](#apache-20) | Copyright (c) 2004-2022 Castle Project - http://www.castleproject.org/ |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector/6.0.4) | 6.0.4 | [MIT](#mit) | Copyright (c) 2018 Toni Solarin-Sodara |
| [coverlet.msbuild](https://www.nuget.org/packages/coverlet.msbuild/6.0.4) | 6.0.4 | [MIT](#mit) | Copyright (c) 2018 Toni Solarin-Sodara |
| [Docker.DotNet.Enhanced](https://www.nuget.org/packages/Docker.DotNet.Enhanced/4.3.3) | 4.3.3 | [MIT](#mit) | Copyright (c) .NET Foundation and Contributors; Copyright (c) Andre Hofmeister. All rights reserved. |
| [Docker.DotNet.Enhanced.Handler.Abstractions](https://www.nuget.org/packages/Docker.DotNet.Enhanced.Handler.Abstractions/4.3.3) | 4.3.3 | [MIT](#mit) | Copyright (c) .NET Foundation and Contributors; Copyright (c) Andre Hofmeister. All rights reserved. |
| [Docker.DotNet.Enhanced.LegacyHttp](https://www.nuget.org/packages/Docker.DotNet.Enhanced.LegacyHttp/4.3.3) | 4.3.3 | [MIT](#mit) | Copyright (c) .NET Foundation and Contributors; Copyright (c) Andre Hofmeister. All rights reserved. |
| [Docker.DotNet.Enhanced.NativeHttp](https://www.nuget.org/packages/Docker.DotNet.Enhanced.NativeHttp/4.3.3) | 4.3.3 | [MIT](#mit) | Copyright (c) .NET Foundation and Contributors; Copyright (c) Andre Hofmeister. All rights reserved. |
| [Docker.DotNet.Enhanced.NPipe](https://www.nuget.org/packages/Docker.DotNet.Enhanced.NPipe/4.3.3) | 4.3.3 | [MIT](#mit) | Copyright (c) .NET Foundation and Contributors; Copyright (c) Andre Hofmeister. All rights reserved. |
| [Docker.DotNet.Enhanced.Unix](https://www.nuget.org/packages/Docker.DotNet.Enhanced.Unix/4.3.3) | 4.3.3 | [MIT](#mit) | Copyright (c) .NET Foundation and Contributors; Copyright (c) Andre Hofmeister. All rights reserved. |
| [Docker.DotNet.Enhanced.X509](https://www.nuget.org/packages/Docker.DotNet.Enhanced.X509/4.3.3) | 4.3.3 | [MIT](#mit) | Copyright (c) .NET Foundation and Contributors; Copyright (c) Andre Hofmeister. All rights reserved. |
| [FakeItEasy](https://www.nuget.org/packages/FakeItEasy/8.3.0) | 8.3.0 | [MIT](#mit) | Copyright (c) FakeItEasy contributors. (fakeiteasyfx@gmail.com) |
| [Microsoft.CodeCoverage](https://www.nuget.org/packages/Microsoft.CodeCoverage/17.14.1) | 17.14.1 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1) | 17.14.1 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.TestPlatform.ObjectModel](https://www.nuget.org/packages/Microsoft.TestPlatform.ObjectModel/17.14.1) | 17.14.1 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Microsoft.TestPlatform.TestHost](https://www.nuget.org/packages/Microsoft.TestPlatform.TestHost/17.14.1) | 17.14.1 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/13.0.3) | 13.0.3 | [MIT](#mit) | Copyright © James Newton-King 2008 |
| [SharpZipLib](https://www.nuget.org/packages/SharpZipLib/1.4.2) | 1.4.2 | [MIT](#mit) | Copyright © 2000-2022 SharpZipLib Contributors |
| [SSH.NET](https://www.nuget.org/packages/SSH.NET/2026.0.0) | 2026.0.0 | [MIT](#mit) | Copyright © Renci 2010-2026 |
| [System.Diagnostics.EventLog](https://www.nuget.org/packages/System.Diagnostics.EventLog/6.0.0) | 6.0.0 | [MIT](#mit) | © Microsoft Corporation. All rights reserved. |
| [Testcontainers](https://www.nuget.org/packages/Testcontainers/4.16.0) | 4.16.0 | [MIT](#mit) | Copyright (c) 2019 - 2026 Andre Hofmeister and other authors |
| [Testcontainers.MongoDb](https://www.nuget.org/packages/Testcontainers.MongoDb/4.16.0) | 4.16.0 | [MIT](#mit) | Copyright (c) 2019 - 2026 Andre Hofmeister and other authors |
| [xunit](https://www.nuget.org/packages/xunit/2.9.3) | 2.9.3 | [Apache-2.0](#apache-20) | Copyright (C) .NET Foundation |
| [xunit.abstractions](https://www.nuget.org/packages/xunit.abstractions/2.0.3) | 2.0.3 | [Apache-2.0](#apache-20) | Copyright (c) .NET Foundation and Contributors. All Rights Reserved |
| [xunit.analyzers](https://www.nuget.org/packages/xunit.analyzers/1.18.0) | 1.18.0 | [Apache-2.0](#apache-20) | Copyright (C) .NET Foundation |
| [xunit.assert](https://www.nuget.org/packages/xunit.assert/2.9.3) | 2.9.3 | [Apache-2.0](#apache-20) | Copyright (C) .NET Foundation |
| [xunit.core](https://www.nuget.org/packages/xunit.core/2.9.3) | 2.9.3 | [Apache-2.0](#apache-20) | Copyright (C) .NET Foundation |
| [xunit.extensibility.core](https://www.nuget.org/packages/xunit.extensibility.core/2.9.3) | 2.9.3 | [Apache-2.0](#apache-20) | Copyright (C) .NET Foundation |
| [xunit.extensibility.execution](https://www.nuget.org/packages/xunit.extensibility.execution/2.9.3) | 2.9.3 | [Apache-2.0](#apache-20) | Copyright (C) .NET Foundation |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5) | 3.1.5 | [Apache-2.0](#apache-20) | Copyright (C) .NET Foundation |

`xunit.abstractions` 2.0.3 predates NuGet SPDX metadata. Its Apache-2.0 license is confirmed by the [upstream license](https://github.com/xunit/abstractions.xunit/blob/main/license.txt).

## Bundled components and upstream notices

- **SQLite**, included by `SQLitePCLRaw.lib.e_sqlite3`, is [public domain](https://www.sqlite.org/copyright.html). The Apache-2.0 entries above describe the SQLitePCLRaw packages.
- **Oniguruma**, included by `Onigwrap`, uses the BSD-style license reproduced under [Oniguruma](#oniguruma). The [Onigwrap package](https://www.nuget.org/packages/Onigwrap/1.0.11)'s bundled `THIRD-PARTY-NOTICES.TXT` also credit the following MIT components: fluentCODE/onigwrap — Copyright (c) 2015 Fluent Solutions; TextMateSharp — Copyright (c) 2021 Daniel Peñalba; mono/SkiaSharp — Copyright (c) 2015-2016 Xamarin, Inc. and Copyright (c) 2017-2018 Microsoft Corporation. The MIT text below applies to each of these notices.
- **TextMateSharp.Grammars** bundles third-party language grammars. Their individual notices and licenses are retained in the [upstream grammar sources](https://github.com/danipen/TextMateSharp/tree/622d1b240a5be474939857c0b2780249022f14a3); the MIT entry above describes the NuGet package.
- **Microsoft packages** can include additional `THIRD-PARTY-NOTICES.TXT` / `ThirdPartyNotices.txt` files. Retain the notices applicable to redistributed components, including the [.NET runtime notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT) for self-contained builds and [VSTest notices](https://github.com/microsoft/vstest/blob/490850ae3fdc1b470e3804ceab4f6a41cf89ae51/ThirdPartyNotices.txt) when redistributing test tooling. This NuGet inventory does not enumerate every component embedded in those distributions.

MongoDB Server, MongoDB Shell (`mongosh`), MongoDB Database Tools, OpenSSH, Docker and operating-system libraries are installed separately. Their own licenses apply; they are outside this NuGet inventory.

## License texts

### MIT

Apply this text with the attribution notice for each MIT component above.

```text
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Apache-2.0

Source: [Apache Software Foundation](https://www.apache.org/licenses/LICENSE-2.0).

```text
Apache License, Version 2.0

Apache License
Version 2.0, January 2004
http://www.apache.org/licenses/

TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

1. Definitions.

"License" shall mean the terms and conditions for use, reproduction, and distribution as defined by Sections 1 through 9 of this document.

"Licensor" shall mean the copyright owner or entity authorized by the copyright owner that is granting the License.

"Legal Entity" shall mean the union of the acting entity and all other entities that control, are controlled by, or are under common control with that entity. For the purposes of this definition, "control" means (i) the power, direct or indirect, to cause the direction or management of such entity, whether by contract or otherwise, or (ii) ownership of fifty percent (50%) or more of the outstanding shares, or (iii) beneficial ownership of such entity.

"You" (or "Your") shall mean an individual or Legal Entity exercising permissions granted by this License.

"Source" form shall mean the preferred form for making modifications, including but not limited to software source code, documentation source, and configuration files.

"Object" form shall mean any form resulting from mechanical transformation or translation of a Source form, including but not limited to compiled object code, generated documentation, and conversions to other media types.

"Work" shall mean the work of authorship, whether in Source or Object form, made available under the License, as indicated by a copyright notice that is included in or attached to the work (an example is provided in the Appendix below).

"Derivative Works" shall mean any work, whether in Source or Object form, that is based on (or derived from) the Work and for which the editorial revisions, annotations, elaborations, or other modifications represent, as a whole, an original work of authorship. For the purposes of this License, Derivative Works shall not include works that remain separable from, or merely link (or bind by name) to the interfaces of, the Work and Derivative Works thereof.

"Contribution" shall mean any work of authorship, including the original version of the Work and any modifications or additions to that Work or Derivative Works thereof, that is intentionally submitted to Licensor for inclusion in the Work by the copyright owner or by an individual or Legal Entity authorized to submit on behalf of the copyright owner. For the purposes of this definition, "submitted" means any form of electronic, verbal, or written communication sent to the Licensor or its representatives, including but not limited to communication on electronic mailing lists, source code control systems, and issue tracking systems that are managed by, or on behalf of, the Licensor for the purpose of discussing and improving the Work, but excluding communication that is conspicuously marked or otherwise designated in writing by the copyright owner as "Not a Contribution."

"Contributor" shall mean Licensor and any individual or Legal Entity on behalf of whom a Contribution has been received by Licensor and subsequently incorporated within the Work.

2. Grant of Copyright License. Subject to the terms and conditions of this License, each Contributor hereby grants to You a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable copyright license to reproduce, prepare Derivative Works of, publicly display, publicly perform, sublicense, and distribute the Work and such Derivative Works in Source or Object form.

3. Grant of Patent License. Subject to the terms and conditions of this License, each Contributor hereby grants to You a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable (except as stated in this section) patent license to make, have made, use, offer to sell, sell, import, and otherwise transfer the Work, where such license applies only to those patent claims licensable by such Contributor that are necessarily infringed by their Contribution(s) alone or by combination of their Contribution(s) with the Work to which such Contribution(s) was submitted. If You institute patent litigation against any entity (including a cross-claim or counterclaim in a lawsuit) alleging that the Work or a Contribution incorporated within the Work constitutes direct or contributory patent infringement, then any patent licenses granted to You under this License for that Work shall terminate as of the date such litigation is filed.

4. Redistribution. You may reproduce and distribute copies of the Work or Derivative Works thereof in any medium, with or without modifications, and in Source or Object form, provided that You meet the following conditions:

   1. You must give any other recipients of the Work or Derivative Works a copy of this License; and

   2. You must cause any modified files to carry prominent notices stating that You changed the files; and

   3. You must retain, in the Source form of any Derivative Works that You distribute, all copyright, patent, trademark, and attribution notices from the Source form of the Work, excluding those notices that do not pertain to any part of the Derivative Works; and

   4. If the Work includes a "NOTICE" text file as part of its distribution, then any Derivative Works that You distribute must include a readable copy of the attribution notices contained within such NOTICE file, excluding those notices that do not pertain to any part of the Derivative Works, in at least one of the following places: within a NOTICE text file distributed as part of the Derivative Works; within the Source form or documentation, if provided along with the Derivative Works; or, within a display generated by the Derivative Works, if and wherever such third-party notices normally appear. The contents of the NOTICE file are for informational purposes only and do not modify the License. You may add Your own attribution notices within Derivative Works that You distribute, alongside or as an addendum to the NOTICE text from the Work, provided that such additional attribution notices cannot be construed as modifying the License.

You may add Your own copyright statement to Your modifications and may provide additional or different license terms and conditions for use, reproduction, or distribution of Your modifications, or for any such Derivative Works as a whole, provided Your use, reproduction, and distribution of the Work otherwise complies with the conditions stated in this License.

5. Submission of Contributions. Unless You explicitly state otherwise, any Contribution intentionally submitted for inclusion in the Work by You to the Licensor shall be under the terms and conditions of this License, without any additional terms or conditions. Notwithstanding the above, nothing herein shall supersede or modify the terms of any separate license agreement you may have executed with Licensor regarding such Contributions.

6. Trademarks. This License does not grant permission to use the trade names, trademarks, service marks, or product names of the Licensor, except as required for reasonable and customary use in describing the origin of the Work and reproducing the content of the NOTICE file.

7. Disclaimer of Warranty. Unless required by applicable law or agreed to in writing, Licensor provides the Work (and each Contributor provides its Contributions) on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied, including, without limitation, any warranties or conditions of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A PARTICULAR PURPOSE. You are solely responsible for determining the appropriateness of using or redistributing the Work and assume any risks associated with Your exercise of permissions under this License.

8. Limitation of Liability. In no event and under no legal theory, whether in tort (including negligence), contract, or otherwise, unless required by applicable law (such as deliberate and grossly negligent acts) or agreed to in writing, shall any Contributor be liable to You for damages, including any direct, indirect, special, incidental, or consequential damages of any character arising as a result of this License or out of the use or inability to use the Work (including but not limited to damages for loss of goodwill, work stoppage, computer failure or malfunction, or any and all other commercial damages or losses), even if such Contributor has been advised of the possibility of such damages.

9. Accepting Warranty or Additional Liability. While redistributing the Work or Derivative Works thereof, You may choose to offer, and charge a fee for, acceptance of support, warranty, indemnity, or other liability obligations and/or rights consistent with this License. However, in accepting such obligations, You may act only on Your own behalf and on Your sole responsibility, not on behalf of any other Contributor, and only if You agree to indemnify, defend, and hold each Contributor harmless for any liability incurred by, or claims asserted against, such Contributor by reason of your accepting any such warranty or additional liability.

END OF TERMS AND CONDITIONS

APPENDIX: How to apply the Apache License to your work.

To apply the Apache License to your work, attach the following boilerplate notice, with the fields enclosed by brackets "[]" replaced with your own identifying information. (Don't include the brackets!) The text should be enclosed in the appropriate comment syntax for the file format. We also recommend that a file or class name and description of purpose be included on the same "printed page" as the copyright notice for easier identification within third-party archives.

Copyright [yyyy] [name of copyright owner]

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

### BSD-2-Clause

Markdig — Copyright (c) 2018-2019, Alexandre Mutel. All rights reserved.
Source: [Markdig license](https://github.com/xoofx/markdig/blob/fc705234fa211d179ee1d5e7656b51ab99f70ca9/license.txt).

```text
Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

### BSD-3-Clause

Snappier — Copyright 2011-2020, Google, Inc. and Snappier Authors. All rights reserved.
Source: [NuGet license metadata](https://www.nuget.org/packages/Snappier/1.3.1), [BSD-3-Clause text](https://licenses.nuget.org/BSD-3-Clause).

```text
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors
   may be used to endorse or promote products derived from this software
   without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

### Oniguruma

Reproduced from `Onigwrap` 1.0.11's `THIRD-PARTY-NOTICES.TXT`.

```text
Copyright (c) 2002-2021  K.Kosako  <kkosako0@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS ``AS IS'' AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
SUCH DAMAGE.
```

## Updating this inventory

After changing dependencies, restore the solution and inspect `dotnet list MongoTui.sln package --include-transitive --no-restore` together with each resolved package's `.nuspec` and bundled notices in the NuGet global-packages folder. Update versions, attribution and license texts as needed. Keep `LICENSE`, this file and applicable upstream notices with redistributed copies.
