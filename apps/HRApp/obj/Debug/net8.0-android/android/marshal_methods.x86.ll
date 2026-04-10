; ModuleID = 'marshal_methods.x86.ll'
source_filename = "marshal_methods.x86.ll"
target datalayout = "e-m:e-p:32:32-p270:32:32-p271:32:32-p272:64:64-f64:32:64-f80:32-n8:16:32-S128"
target triple = "i686-unknown-linux-android21"

%struct.MarshalMethodName = type {
	i64, ; uint64_t id
	ptr ; char* name
}

%struct.MarshalMethodsManagedClass = type {
	i32, ; uint32_t token
	ptr ; MonoClass klass
}

@assembly_image_cache = dso_local local_unnamed_addr global [351 x ptr] zeroinitializer, align 4

; Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array
@assembly_image_cache_hashes = dso_local local_unnamed_addr constant [696 x i32] [
	i32 2616222, ; 0: System.Net.NetworkInformation.dll => 0x27eb9e => 68
	i32 10166715, ; 1: System.Net.NameResolution.dll => 0x9b21bb => 67
	i32 15721112, ; 2: System.Runtime.Intrinsics.dll => 0xefe298 => 108
	i32 26230656, ; 3: Microsoft.Extensions.DependencyModel => 0x1903f80 => 195
	i32 32687329, ; 4: Xamarin.AndroidX.Lifecycle.Runtime => 0x1f2c4e1 => 270
	i32 34715100, ; 5: Xamarin.Google.Guava.ListenableFuture.dll => 0x211b5dc => 304
	i32 34839235, ; 6: System.IO.FileSystem.DriveInfo => 0x2139ac3 => 48
	i32 39485524, ; 7: System.Net.WebSockets.dll => 0x25a8054 => 80
	i32 42639949, ; 8: System.Threading.Thread => 0x28aa24d => 145
	i32 66541672, ; 9: System.Diagnostics.StackTrace => 0x3f75868 => 30
	i32 67008169, ; 10: zh-Hant\Microsoft.Maui.Controls.resources => 0x3fe76a9 => 345
	i32 68219467, ; 11: System.Security.Cryptography.Primitives => 0x410f24b => 124
	i32 72070932, ; 12: Microsoft.Maui.Graphics.dll => 0x44bb714 => 217
	i32 82292897, ; 13: System.Runtime.CompilerServices.VisualC.dll => 0x4e7b0a1 => 102
	i32 98325684, ; 14: Microsoft.Extensions.Diagnostics.Abstractions => 0x5dc54b4 => 197
	i32 101534019, ; 15: Xamarin.AndroidX.SlidingPaneLayout => 0x60d4943 => 288
	i32 117431740, ; 16: System.Runtime.InteropServices => 0x6ffddbc => 107
	i32 120558881, ; 17: Xamarin.AndroidX.SlidingPaneLayout.dll => 0x72f9521 => 288
	i32 122350210, ; 18: System.Threading.Channels.dll => 0x74aea82 => 139
	i32 134690465, ; 19: Xamarin.Kotlin.StdLib.Jdk7.dll => 0x80736a1 => 308
	i32 142721839, ; 20: System.Net.WebHeaderCollection => 0x881c32f => 77
	i32 149972175, ; 21: System.Security.Cryptography.Primitives.dll => 0x8f064cf => 124
	i32 159306688, ; 22: System.ComponentModel.Annotations => 0x97ed3c0 => 13
	i32 165246403, ; 23: Xamarin.AndroidX.Collection.dll => 0x9d975c3 => 244
	i32 176265551, ; 24: System.ServiceProcess => 0xa81994f => 132
	i32 182336117, ; 25: Xamarin.AndroidX.SwipeRefreshLayout.dll => 0xade3a75 => 290
	i32 184328833, ; 26: System.ValueTuple.dll => 0xafca281 => 151
	i32 195452805, ; 27: vi/Microsoft.Maui.Controls.resources.dll => 0xba65f85 => 342
	i32 199333315, ; 28: zh-HK/Microsoft.Maui.Controls.resources.dll => 0xbe195c3 => 343
	i32 205061960, ; 29: System.ComponentModel => 0xc38ff48 => 18
	i32 209399409, ; 30: Xamarin.AndroidX.Browser.dll => 0xc7b2e71 => 242
	i32 220171995, ; 31: System.Diagnostics.Debug => 0xd1f8edb => 26
	i32 221063263, ; 32: Microsoft.AspNetCore.Http.Connections.Client => 0xd2d285f => 175
	i32 221958352, ; 33: Microsoft.Extensions.Diagnostics.dll => 0xd3ad0d0 => 196
	i32 230216969, ; 34: Xamarin.AndroidX.Legacy.Support.Core.Utils.dll => 0xdb8d509 => 264
	i32 230752869, ; 35: Microsoft.CSharp.dll => 0xdc10265 => 1
	i32 231409092, ; 36: System.Linq.Parallel => 0xdcb05c4 => 59
	i32 231814094, ; 37: System.Globalization => 0xdd133ce => 42
	i32 246610117, ; 38: System.Reflection.Emit.Lightweight => 0xeb2f8c5 => 91
	i32 261689757, ; 39: Xamarin.AndroidX.ConstraintLayout.dll => 0xf99119d => 247
	i32 276479776, ; 40: System.Threading.Timer.dll => 0x107abf20 => 147
	i32 278686392, ; 41: Xamarin.AndroidX.Lifecycle.LiveData.dll => 0x109c6ab8 => 266
	i32 280482487, ; 42: Xamarin.AndroidX.Interpolator => 0x10b7d2b7 => 263
	i32 280992041, ; 43: cs/Microsoft.Maui.Controls.resources.dll => 0x10bf9929 => 314
	i32 291076382, ; 44: System.IO.Pipes.AccessControl.dll => 0x1159791e => 54
	i32 291275502, ; 45: Microsoft.Extensions.Http.dll => 0x115c82ee => 205
	i32 298918909, ; 46: System.Net.Ping.dll => 0x11d123fd => 69
	i32 317674968, ; 47: vi\Microsoft.Maui.Controls.resources => 0x12ef55d8 => 342
	i32 318968648, ; 48: Xamarin.AndroidX.Activity.dll => 0x13031348 => 233
	i32 321597661, ; 49: System.Numerics => 0x132b30dd => 83
	i32 336156722, ; 50: ja/Microsoft.Maui.Controls.resources.dll => 0x14095832 => 327
	i32 342366114, ; 51: Xamarin.AndroidX.Lifecycle.Common => 0x146817a2 => 265
	i32 347068432, ; 52: SQLitePCLRaw.lib.e_sqlite3.android.dll => 0x14afd810 => 226
	i32 348048101, ; 53: Microsoft.AspNetCore.Http.Connections.Common.dll => 0x14becae5 => 176
	i32 356389973, ; 54: it/Microsoft.Maui.Controls.resources.dll => 0x153e1455 => 326
	i32 360082299, ; 55: System.ServiceModel.Web => 0x15766b7b => 131
	i32 367780167, ; 56: System.IO.Pipes => 0x15ebe147 => 55
	i32 374914964, ; 57: System.Transactions.Local => 0x1658bf94 => 149
	i32 375677976, ; 58: System.Net.ServicePoint.dll => 0x16646418 => 74
	i32 379916513, ; 59: System.Threading.Thread.dll => 0x16a510e1 => 145
	i32 381586349, ; 60: Microsoft.Extensions.Diagnostics.HealthChecks => 0x16be8bad => 198
	i32 385762202, ; 61: System.Memory.dll => 0x16fe439a => 62
	i32 390050989, ; 62: Serilog.Extensions.Hosting.dll => 0x173fb4ad => 219
	i32 392610295, ; 63: System.Threading.ThreadPool.dll => 0x1766c1f7 => 146
	i32 395744057, ; 64: _Microsoft.Android.Resource.Designer => 0x17969339 => 347
	i32 398680804, ; 65: Serilog.Sinks.Console => 0x17c362e4 => 221
	i32 403441872, ; 66: WindowsBase => 0x180c08d0 => 165
	i32 435591531, ; 67: sv/Microsoft.Maui.Controls.resources.dll => 0x19f6996b => 338
	i32 441335492, ; 68: Xamarin.AndroidX.ConstraintLayout.Core => 0x1a4e3ec4 => 248
	i32 442565967, ; 69: System.Collections => 0x1a61054f => 12
	i32 450948140, ; 70: Xamarin.AndroidX.Fragment.dll => 0x1ae0ec2c => 261
	i32 451504562, ; 71: System.Security.Cryptography.X509Certificates => 0x1ae969b2 => 125
	i32 456227837, ; 72: System.Web.HttpUtility.dll => 0x1b317bfd => 152
	i32 458494020, ; 73: Microsoft.AspNetCore.SignalR.Common.dll => 0x1b541044 => 179
	i32 459347974, ; 74: System.Runtime.Serialization.Primitives.dll => 0x1b611806 => 113
	i32 465846621, ; 75: mscorlib => 0x1bc4415d => 166
	i32 469710990, ; 76: System.dll => 0x1bff388e => 164
	i32 476646585, ; 77: Xamarin.AndroidX.Interpolator.dll => 0x1c690cb9 => 263
	i32 486930444, ; 78: Xamarin.AndroidX.LocalBroadcastManager.dll => 0x1d05f80c => 276
	i32 498788369, ; 79: System.ObjectModel => 0x1dbae811 => 84
	i32 500358224, ; 80: id/Microsoft.Maui.Controls.resources.dll => 0x1dd2dc50 => 325
	i32 503918385, ; 81: fi/Microsoft.Maui.Controls.resources.dll => 0x1e092f31 => 319
	i32 513247710, ; 82: Microsoft.Extensions.Primitives.dll => 0x1e9789de => 211
	i32 526420162, ; 83: System.Transactions.dll => 0x1f6088c2 => 150
	i32 527452488, ; 84: Xamarin.Kotlin.StdLib.Jdk7 => 0x1f704948 => 308
	i32 530272170, ; 85: System.Linq.Queryable => 0x1f9b4faa => 60
	i32 539058512, ; 86: Microsoft.Extensions.Logging => 0x20216150 => 206
	i32 540030774, ; 87: System.IO.FileSystem.dll => 0x20303736 => 51
	i32 545304856, ; 88: System.Runtime.Extensions => 0x2080b118 => 103
	i32 546455878, ; 89: System.Runtime.Serialization.Xml => 0x20924146 => 114
	i32 549171840, ; 90: System.Globalization.Calendars => 0x20bbb280 => 40
	i32 557405415, ; 91: Jsr305Binding => 0x213954e7 => 301
	i32 565050267, ; 92: Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions.dll => 0x21adfb9b => 199
	i32 569601784, ; 93: Xamarin.AndroidX.Window.Extensions.Core.Core => 0x21f36ef8 => 299
	i32 577335427, ; 94: System.Security.Cryptography.Cng => 0x22697083 => 120
	i32 592146354, ; 95: pt-BR/Microsoft.Maui.Controls.resources.dll => 0x234b6fb2 => 333
	i32 601371474, ; 96: System.IO.IsolatedStorage.dll => 0x23d83352 => 52
	i32 605376203, ; 97: System.IO.Compression.FileSystem => 0x24154ecb => 44
	i32 613668793, ; 98: System.Security.Cryptography.Algorithms => 0x2493d7b9 => 119
	i32 617799481, ; 99: VanAn.HRApp => 0x24d2df39 => 0
	i32 627609679, ; 100: Xamarin.AndroidX.CustomView => 0x2568904f => 253
	i32 627931235, ; 101: nl\Microsoft.Maui.Controls.resources => 0x256d7863 => 331
	i32 639481687, ; 102: Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions => 0x261db757 => 199
	i32 639843206, ; 103: Xamarin.AndroidX.Emoji2.ViewsHelper.dll => 0x26233b86 => 259
	i32 643868501, ; 104: System.Net => 0x2660a755 => 81
	i32 662205335, ; 105: System.Text.Encodings.Web.dll => 0x27787397 => 136
	i32 663517072, ; 106: Xamarin.AndroidX.VersionedParcelable => 0x278c7790 => 295
	i32 666292255, ; 107: Xamarin.AndroidX.Arch.Core.Common.dll => 0x27b6d01f => 240
	i32 672442732, ; 108: System.Collections.Concurrent => 0x2814a96c => 8
	i32 683518922, ; 109: System.Net.Security => 0x28bdabca => 73
	i32 688181140, ; 110: ca/Microsoft.Maui.Controls.resources.dll => 0x2904cf94 => 313
	i32 690569205, ; 111: System.Xml.Linq.dll => 0x29293ff5 => 155
	i32 691348768, ; 112: Xamarin.KotlinX.Coroutines.Android.dll => 0x29352520 => 310
	i32 693804605, ; 113: System.Windows => 0x295a9e3d => 154
	i32 699345723, ; 114: System.Reflection.Emit => 0x29af2b3b => 92
	i32 700284507, ; 115: Xamarin.Jetbrains.Annotations => 0x29bd7e5b => 305
	i32 700358131, ; 116: System.IO.Compression.ZipFile => 0x29be9df3 => 45
	i32 706645707, ; 117: ko/Microsoft.Maui.Controls.resources.dll => 0x2a1e8ecb => 328
	i32 709557578, ; 118: de/Microsoft.Maui.Controls.resources.dll => 0x2a4afd4a => 316
	i32 720511267, ; 119: Xamarin.Kotlin.StdLib.Jdk8 => 0x2af22123 => 309
	i32 722857257, ; 120: System.Runtime.Loader.dll => 0x2b15ed29 => 109
	i32 731701662, ; 121: Microsoft.Extensions.Options.ConfigurationExtensions => 0x2b9ce19e => 210
	i32 735137430, ; 122: System.Security.SecureString.dll => 0x2bd14e96 => 129
	i32 748832960, ; 123: SQLitePCLRaw.batteries_v2 => 0x2ca248c0 => 224
	i32 752232764, ; 124: System.Diagnostics.Contracts.dll => 0x2cd6293c => 25
	i32 755313932, ; 125: Xamarin.Android.Glide.Annotations.dll => 0x2d052d0c => 230
	i32 759454413, ; 126: System.Net.Requests => 0x2d445acd => 72
	i32 762598435, ; 127: System.IO.Pipes.dll => 0x2d745423 => 55
	i32 775507847, ; 128: System.IO.Compression => 0x2e394f87 => 46
	i32 777317022, ; 129: sk\Microsoft.Maui.Controls.resources => 0x2e54ea9e => 337
	i32 789151979, ; 130: Microsoft.Extensions.Options => 0x2f0980eb => 209
	i32 790371945, ; 131: Xamarin.AndroidX.CustomView.PoolingContainer.dll => 0x2f1c1e69 => 254
	i32 804715423, ; 132: System.Data.Common => 0x2ff6fb9f => 22
	i32 807930345, ; 133: Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx.dll => 0x302809e9 => 268
	i32 812630446, ; 134: Serilog => 0x306fc1ae => 218
	i32 823281589, ; 135: System.Private.Uri.dll => 0x311247b5 => 86
	i32 830298997, ; 136: System.IO.Compression.Brotli => 0x317d5b75 => 43
	i32 832635846, ; 137: System.Xml.XPath.dll => 0x31a103c6 => 160
	i32 832711436, ; 138: Microsoft.AspNetCore.SignalR.Protocols.Json.dll => 0x31a22b0c => 180
	i32 834051424, ; 139: System.Net.Quic => 0x31b69d60 => 71
	i32 843511501, ; 140: Xamarin.AndroidX.Print => 0x3246f6cd => 281
	i32 873119928, ; 141: Microsoft.VisualBasic => 0x340ac0b8 => 3
	i32 877678880, ; 142: System.Globalization.dll => 0x34505120 => 42
	i32 878954865, ; 143: System.Net.Http.Json => 0x3463c971 => 63
	i32 904024072, ; 144: System.ComponentModel.Primitives.dll => 0x35e25008 => 16
	i32 906306440, ; 145: VanAn.HRApp.dll => 0x36052388 => 0
	i32 911108515, ; 146: System.IO.MemoryMappedFiles.dll => 0x364e69a3 => 53
	i32 926902833, ; 147: tr/Microsoft.Maui.Controls.resources.dll => 0x373f6a31 => 340
	i32 928116545, ; 148: Xamarin.Google.Guava.ListenableFuture => 0x3751ef41 => 304
	i32 952186615, ; 149: System.Runtime.InteropServices.JavaScript.dll => 0x38c136f7 => 105
	i32 956575887, ; 150: Xamarin.Kotlin.StdLib.Jdk8.dll => 0x3904308f => 309
	i32 966729478, ; 151: Xamarin.Google.Crypto.Tink.Android => 0x399f1f06 => 302
	i32 967690846, ; 152: Xamarin.AndroidX.Lifecycle.Common.dll => 0x39adca5e => 265
	i32 975236339, ; 153: System.Diagnostics.Tracing => 0x3a20ecf3 => 34
	i32 975874589, ; 154: System.Xml.XDocument => 0x3a2aaa1d => 158
	i32 986514023, ; 155: System.Private.DataContractSerialization.dll => 0x3acd0267 => 85
	i32 987214855, ; 156: System.Diagnostics.Tools => 0x3ad7b407 => 32
	i32 992768348, ; 157: System.Collections.dll => 0x3b2c715c => 12
	i32 994442037, ; 158: System.IO.FileSystem => 0x3b45fb35 => 51
	i32 999186168, ; 159: Microsoft.Extensions.FileSystemGlobbing.dll => 0x3b8e5ef8 => 203
	i32 1001831731, ; 160: System.IO.UnmanagedMemoryStream.dll => 0x3bb6bd33 => 56
	i32 1012816738, ; 161: Xamarin.AndroidX.SavedState.dll => 0x3c5e5b62 => 285
	i32 1019214401, ; 162: System.Drawing => 0x3cbffa41 => 36
	i32 1028951442, ; 163: Microsoft.Extensions.DependencyInjection.Abstractions => 0x3d548d92 => 194
	i32 1029334545, ; 164: da/Microsoft.Maui.Controls.resources.dll => 0x3d5a6611 => 315
	i32 1031528504, ; 165: Xamarin.Google.ErrorProne.Annotations.dll => 0x3d7be038 => 303
	i32 1035644815, ; 166: Xamarin.AndroidX.AppCompat => 0x3dbaaf8f => 238
	i32 1036536393, ; 167: System.Drawing.Primitives.dll => 0x3dc84a49 => 35
	i32 1044663988, ; 168: System.Linq.Expressions.dll => 0x3e444eb4 => 58
	i32 1048992957, ; 169: Microsoft.Extensions.Diagnostics.Abstractions.dll => 0x3e865cbd => 197
	i32 1052210849, ; 170: Xamarin.AndroidX.Lifecycle.ViewModel.dll => 0x3eb776a1 => 272
	i32 1058641855, ; 171: Microsoft.AspNetCore.Http.Connections.Common => 0x3f1997bf => 176
	i32 1067306892, ; 172: GoogleGson => 0x3f9dcf8c => 173
	i32 1082857460, ; 173: System.ComponentModel.TypeConverter => 0x408b17f4 => 17
	i32 1084122840, ; 174: Xamarin.Kotlin.StdLib => 0x409e66d8 => 306
	i32 1098259244, ; 175: System => 0x41761b2c => 164
	i32 1106973742, ; 176: Microsoft.Extensions.Configuration.FileExtensions.dll => 0x41fb142e => 191
	i32 1110309514, ; 177: Microsoft.Extensions.Hosting.Abstractions => 0x422dfa8a => 204
	i32 1118262833, ; 178: ko\Microsoft.Maui.Controls.resources => 0x42a75631 => 328
	i32 1121599056, ; 179: Xamarin.AndroidX.Lifecycle.Runtime.Ktx.dll => 0x42da3e50 => 271
	i32 1127624469, ; 180: Microsoft.Extensions.Logging.Debug => 0x43362f15 => 208
	i32 1149092582, ; 181: Xamarin.AndroidX.Window => 0x447dc2e6 => 298
	i32 1157931901, ; 182: Microsoft.EntityFrameworkCore.Abstractions => 0x4504a37d => 183
	i32 1168523401, ; 183: pt\Microsoft.Maui.Controls.resources => 0x45a64089 => 334
	i32 1170634674, ; 184: System.Web.dll => 0x45c677b2 => 153
	i32 1173126369, ; 185: Microsoft.Extensions.FileProviders.Abstractions.dll => 0x45ec7ce1 => 201
	i32 1175144683, ; 186: Xamarin.AndroidX.VectorDrawable.Animated => 0x460b48eb => 294
	i32 1178241025, ; 187: Xamarin.AndroidX.Navigation.Runtime.dll => 0x463a8801 => 279
	i32 1202000627, ; 188: Microsoft.EntityFrameworkCore.Abstractions.dll => 0x47a512f3 => 183
	i32 1203215381, ; 189: pl/Microsoft.Maui.Controls.resources.dll => 0x47b79c15 => 332
	i32 1204270330, ; 190: Xamarin.AndroidX.Arch.Core.Common => 0x47c7b4fa => 240
	i32 1204575371, ; 191: Microsoft.Extensions.Caching.Memory.dll => 0x47cc5c8b => 187
	i32 1208641965, ; 192: System.Diagnostics.Process => 0x480a69ad => 29
	i32 1219128291, ; 193: System.IO.IsolatedStorage => 0x48aa6be3 => 52
	i32 1233093933, ; 194: Microsoft.AspNetCore.SignalR.Client.Core.dll => 0x497f852d => 178
	i32 1234928153, ; 195: nb/Microsoft.Maui.Controls.resources.dll => 0x499b8219 => 330
	i32 1243150071, ; 196: Xamarin.AndroidX.Window.Extensions.Core.Core.dll => 0x4a18f6f7 => 299
	i32 1253011324, ; 197: Microsoft.Win32.Registry => 0x4aaf6f7c => 5
	i32 1260983243, ; 198: cs\Microsoft.Maui.Controls.resources => 0x4b2913cb => 314
	i32 1264511973, ; 199: Xamarin.AndroidX.Startup.StartupRuntime.dll => 0x4b5eebe5 => 289
	i32 1267360935, ; 200: Xamarin.AndroidX.VectorDrawable => 0x4b8a64a7 => 293
	i32 1273260888, ; 201: Xamarin.AndroidX.Collection.Ktx => 0x4be46b58 => 245
	i32 1275534314, ; 202: Xamarin.KotlinX.Coroutines.Android => 0x4c071bea => 310
	i32 1278448581, ; 203: Xamarin.AndroidX.Annotation.Jvm => 0x4c3393c5 => 237
	i32 1292207520, ; 204: SQLitePCLRaw.core.dll => 0x4d0585a0 => 225
	i32 1293217323, ; 205: Xamarin.AndroidX.DrawerLayout.dll => 0x4d14ee2b => 256
	i32 1309188875, ; 206: System.Private.DataContractSerialization => 0x4e08a30b => 85
	i32 1322716291, ; 207: Xamarin.AndroidX.Window.dll => 0x4ed70c83 => 298
	i32 1322857724, ; 208: Serilog.Sinks.File.dll => 0x4ed934fc => 222
	i32 1324164729, ; 209: System.Linq => 0x4eed2679 => 61
	i32 1335329327, ; 210: System.Runtime.Serialization.Json.dll => 0x4f97822f => 112
	i32 1364015309, ; 211: System.IO => 0x514d38cd => 57
	i32 1373134921, ; 212: zh-Hans\Microsoft.Maui.Controls.resources => 0x51d86049 => 344
	i32 1376866003, ; 213: Xamarin.AndroidX.SavedState => 0x52114ed3 => 285
	i32 1379779777, ; 214: System.Resources.ResourceManager => 0x523dc4c1 => 99
	i32 1402170036, ; 215: System.Configuration.dll => 0x53936ab4 => 19
	i32 1406073936, ; 216: Xamarin.AndroidX.CoordinatorLayout => 0x53cefc50 => 249
	i32 1408764838, ; 217: System.Runtime.Serialization.Formatters.dll => 0x53f80ba6 => 111
	i32 1411638395, ; 218: System.Runtime.CompilerServices.Unsafe => 0x5423e47b => 101
	i32 1414043276, ; 219: Microsoft.AspNetCore.Connections.Abstractions.dll => 0x5448968c => 174
	i32 1422545099, ; 220: System.Runtime.CompilerServices.VisualC => 0x54ca50cb => 102
	i32 1430672901, ; 221: ar\Microsoft.Maui.Controls.resources => 0x55465605 => 312
	i32 1434145427, ; 222: System.Runtime.Handles => 0x557b5293 => 104
	i32 1435222561, ; 223: Xamarin.Google.Crypto.Tink.Android.dll => 0x558bc221 => 302
	i32 1439761251, ; 224: System.Net.Quic.dll => 0x55d10363 => 71
	i32 1452070440, ; 225: System.Formats.Asn1.dll => 0x568cd628 => 38
	i32 1453312822, ; 226: System.Diagnostics.Tools.dll => 0x569fcb36 => 32
	i32 1457743152, ; 227: System.Runtime.Extensions.dll => 0x56e36530 => 103
	i32 1458022317, ; 228: System.Net.Security.dll => 0x56e7a7ad => 73
	i32 1461004990, ; 229: es\Microsoft.Maui.Controls.resources => 0x57152abe => 318
	i32 1461234159, ; 230: System.Collections.Immutable.dll => 0x5718a9ef => 9
	i32 1461719063, ; 231: System.Security.Cryptography.OpenSsl => 0x57201017 => 123
	i32 1462112819, ; 232: System.IO.Compression.dll => 0x57261233 => 46
	i32 1469204771, ; 233: Xamarin.AndroidX.AppCompat.AppCompatResources => 0x57924923 => 239
	i32 1470490898, ; 234: Microsoft.Extensions.Primitives => 0x57a5e912 => 211
	i32 1479771757, ; 235: System.Collections.Immutable => 0x5833866d => 9
	i32 1480492111, ; 236: System.IO.Compression.Brotli.dll => 0x583e844f => 43
	i32 1487239319, ; 237: Microsoft.Win32.Primitives => 0x58a57897 => 4
	i32 1490025113, ; 238: Xamarin.AndroidX.SavedState.SavedState.Ktx.dll => 0x58cffa99 => 286
	i32 1490351284, ; 239: Microsoft.Data.Sqlite.dll => 0x58d4f4b4 => 181
	i32 1493001747, ; 240: hi/Microsoft.Maui.Controls.resources.dll => 0x58fd6613 => 322
	i32 1503615124, ; 241: Serilog.Sinks.Seq.dll => 0x599f5894 => 223
	i32 1505131794, ; 242: Microsoft.Extensions.Http => 0x59b67d12 => 205
	i32 1514721132, ; 243: el/Microsoft.Maui.Controls.resources.dll => 0x5a48cf6c => 317
	i32 1521091094, ; 244: Microsoft.Extensions.FileSystemGlobbing => 0x5aaa0216 => 203
	i32 1536373174, ; 245: System.Diagnostics.TextWriterTraceListener => 0x5b9331b6 => 31
	i32 1543031311, ; 246: System.Text.RegularExpressions.dll => 0x5bf8ca0f => 138
	i32 1543355203, ; 247: System.Reflection.Emit.dll => 0x5bfdbb43 => 92
	i32 1550322496, ; 248: System.Reflection.Extensions.dll => 0x5c680b40 => 93
	i32 1551623176, ; 249: sk/Microsoft.Maui.Controls.resources.dll => 0x5c7be408 => 337
	i32 1565862583, ; 250: System.IO.FileSystem.Primitives => 0x5d552ab7 => 49
	i32 1566207040, ; 251: System.Threading.Tasks.Dataflow.dll => 0x5d5a6c40 => 141
	i32 1573704789, ; 252: System.Runtime.Serialization.Json => 0x5dccd455 => 112
	i32 1580037396, ; 253: System.Threading.Overlapped => 0x5e2d7514 => 140
	i32 1582372066, ; 254: Xamarin.AndroidX.DocumentFile.dll => 0x5e5114e2 => 255
	i32 1592978981, ; 255: System.Runtime.Serialization.dll => 0x5ef2ee25 => 115
	i32 1597949149, ; 256: Xamarin.Google.ErrorProne.Annotations => 0x5f3ec4dd => 303
	i32 1601112923, ; 257: System.Xml.Serialization => 0x5f6f0b5b => 157
	i32 1604827217, ; 258: System.Net.WebClient => 0x5fa7b851 => 76
	i32 1618516317, ; 259: System.Net.WebSockets.Client.dll => 0x6078995d => 79
	i32 1622152042, ; 260: Xamarin.AndroidX.Loader.dll => 0x60b0136a => 275
	i32 1622358360, ; 261: System.Dynamic.Runtime => 0x60b33958 => 37
	i32 1624863272, ; 262: Xamarin.AndroidX.ViewPager2 => 0x60d97228 => 297
	i32 1625558452, ; 263: Serilog.dll => 0x60e40db4 => 218
	i32 1632842087, ; 264: Microsoft.Extensions.Configuration.Json => 0x61533167 => 192
	i32 1635184631, ; 265: Xamarin.AndroidX.Emoji2.ViewsHelper => 0x6176eff7 => 259
	i32 1636350590, ; 266: Xamarin.AndroidX.CursorAdapter => 0x6188ba7e => 252
	i32 1639515021, ; 267: System.Net.Http.dll => 0x61b9038d => 64
	i32 1639986890, ; 268: System.Text.RegularExpressions => 0x61c036ca => 138
	i32 1641389582, ; 269: System.ComponentModel.EventBasedAsync.dll => 0x61d59e0e => 15
	i32 1657153582, ; 270: System.Runtime => 0x62c6282e => 116
	i32 1658241508, ; 271: Xamarin.AndroidX.Tracing.Tracing.dll => 0x62d6c1e4 => 291
	i32 1658251792, ; 272: Xamarin.Google.Android.Material.dll => 0x62d6ea10 => 300
	i32 1670060433, ; 273: Xamarin.AndroidX.ConstraintLayout => 0x638b1991 => 247
	i32 1675553242, ; 274: System.IO.FileSystem.DriveInfo.dll => 0x63dee9da => 48
	i32 1677501392, ; 275: System.Net.Primitives.dll => 0x63fca3d0 => 70
	i32 1678508291, ; 276: System.Net.WebSockets => 0x640c0103 => 80
	i32 1679769178, ; 277: System.Security.Cryptography => 0x641f3e5a => 126
	i32 1688112883, ; 278: Microsoft.Data.Sqlite => 0x649e8ef3 => 181
	i32 1689493916, ; 279: Microsoft.EntityFrameworkCore.dll => 0x64b3a19c => 182
	i32 1691477237, ; 280: System.Reflection.Metadata => 0x64d1e4f5 => 94
	i32 1696967625, ; 281: System.Security.Cryptography.Csp => 0x6525abc9 => 121
	i32 1698840827, ; 282: Xamarin.Kotlin.StdLib.Common => 0x654240fb => 307
	i32 1701541528, ; 283: System.Diagnostics.Debug.dll => 0x656b7698 => 26
	i32 1711441057, ; 284: SQLitePCLRaw.lib.e_sqlite3.android => 0x660284a1 => 226
	i32 1720223769, ; 285: Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx => 0x66888819 => 268
	i32 1726116996, ; 286: System.Reflection.dll => 0x66e27484 => 97
	i32 1728033016, ; 287: System.Diagnostics.FileVersionInfo.dll => 0x66ffb0f8 => 28
	i32 1729485958, ; 288: Xamarin.AndroidX.CardView.dll => 0x6715dc86 => 243
	i32 1736233607, ; 289: ro/Microsoft.Maui.Controls.resources.dll => 0x677cd287 => 335
	i32 1743415430, ; 290: ca\Microsoft.Maui.Controls.resources => 0x67ea6886 => 313
	i32 1744735666, ; 291: System.Transactions.Local.dll => 0x67fe8db2 => 149
	i32 1746115085, ; 292: System.IO.Pipelines.dll => 0x68139a0d => 228
	i32 1746316138, ; 293: Mono.Android.Export => 0x6816ab6a => 169
	i32 1750313021, ; 294: Microsoft.Win32.Primitives.dll => 0x6853a83d => 4
	i32 1758240030, ; 295: System.Resources.Reader.dll => 0x68cc9d1e => 98
	i32 1763938596, ; 296: System.Diagnostics.TraceSource.dll => 0x69239124 => 33
	i32 1765942094, ; 297: System.Reflection.Extensions => 0x6942234e => 93
	i32 1766324549, ; 298: Xamarin.AndroidX.SwipeRefreshLayout => 0x6947f945 => 290
	i32 1770582343, ; 299: Microsoft.Extensions.Logging.dll => 0x6988f147 => 206
	i32 1776026572, ; 300: System.Core.dll => 0x69dc03cc => 21
	i32 1777075843, ; 301: System.Globalization.Extensions.dll => 0x69ec0683 => 41
	i32 1780572499, ; 302: Mono.Android.Runtime.dll => 0x6a216153 => 170
	i32 1782862114, ; 303: ms\Microsoft.Maui.Controls.resources => 0x6a445122 => 329
	i32 1788241197, ; 304: Xamarin.AndroidX.Fragment => 0x6a96652d => 261
	i32 1793755602, ; 305: he\Microsoft.Maui.Controls.resources => 0x6aea89d2 => 321
	i32 1808609942, ; 306: Xamarin.AndroidX.Loader => 0x6bcd3296 => 275
	i32 1813058853, ; 307: Xamarin.Kotlin.StdLib.dll => 0x6c111525 => 306
	i32 1813201214, ; 308: Xamarin.Google.Android.Material => 0x6c13413e => 300
	i32 1818569960, ; 309: Xamarin.AndroidX.Navigation.UI.dll => 0x6c652ce8 => 280
	i32 1818787751, ; 310: Microsoft.VisualBasic.Core => 0x6c687fa7 => 2
	i32 1824175904, ; 311: System.Text.Encoding.Extensions => 0x6cbab720 => 134
	i32 1824722060, ; 312: System.Runtime.Serialization.Formatters => 0x6cc30c8c => 111
	i32 1828688058, ; 313: Microsoft.Extensions.Logging.Abstractions.dll => 0x6cff90ba => 207
	i32 1842015223, ; 314: uk/Microsoft.Maui.Controls.resources.dll => 0x6dcaebf7 => 341
	i32 1847515442, ; 315: Xamarin.Android.Glide.Annotations => 0x6e1ed932 => 230
	i32 1853025655, ; 316: sv\Microsoft.Maui.Controls.resources => 0x6e72ed77 => 338
	i32 1858542181, ; 317: System.Linq.Expressions => 0x6ec71a65 => 58
	i32 1870277092, ; 318: System.Reflection.Primitives => 0x6f7a29e4 => 95
	i32 1875935024, ; 319: fr\Microsoft.Maui.Controls.resources => 0x6fd07f30 => 320
	i32 1879696579, ; 320: System.Formats.Tar.dll => 0x7009e4c3 => 39
	i32 1885316902, ; 321: Xamarin.AndroidX.Arch.Core.Runtime.dll => 0x705fa726 => 241
	i32 1886040351, ; 322: Microsoft.EntityFrameworkCore.Sqlite.dll => 0x706ab11f => 185
	i32 1888955245, ; 323: System.Diagnostics.Contracts => 0x70972b6d => 25
	i32 1889954781, ; 324: System.Reflection.Metadata.dll => 0x70a66bdd => 94
	i32 1898237753, ; 325: System.Reflection.DispatchProxy => 0x7124cf39 => 89
	i32 1900610850, ; 326: System.Resources.ResourceManager.dll => 0x71490522 => 99
	i32 1910275211, ; 327: System.Collections.NonGeneric.dll => 0x71dc7c8b => 10
	i32 1939592360, ; 328: System.Private.Xml.Linq => 0x739bd4a8 => 87
	i32 1945717188, ; 329: Microsoft.AspNetCore.SignalR.Client.Core => 0x73f949c4 => 178
	i32 1956758971, ; 330: System.Resources.Writer => 0x74a1c5bb => 100
	i32 1961813231, ; 331: Xamarin.AndroidX.Security.SecurityCrypto.dll => 0x74eee4ef => 287
	i32 1967334205, ; 332: Microsoft.AspNetCore.SignalR.Common => 0x7543233d => 179
	i32 1968388702, ; 333: Microsoft.Extensions.Configuration.dll => 0x75533a5e => 188
	i32 1983156543, ; 334: Xamarin.Kotlin.StdLib.Common.dll => 0x7634913f => 307
	i32 1985761444, ; 335: Xamarin.Android.Glide.GifDecoder => 0x765c50a4 => 232
	i32 2003115576, ; 336: el\Microsoft.Maui.Controls.resources => 0x77651e38 => 317
	i32 2011961780, ; 337: System.Buffers.dll => 0x77ec19b4 => 7
	i32 2014489277, ; 338: Microsoft.EntityFrameworkCore.Sqlite => 0x7812aabd => 185
	i32 2019465201, ; 339: Xamarin.AndroidX.Lifecycle.ViewModel => 0x785e97f1 => 272
	i32 2025202353, ; 340: ar/Microsoft.Maui.Controls.resources.dll => 0x78b622b1 => 312
	i32 2031763787, ; 341: Xamarin.Android.Glide => 0x791a414b => 229
	i32 2045470958, ; 342: System.Private.Xml => 0x79eb68ee => 88
	i32 2048278909, ; 343: Microsoft.Extensions.Configuration.Binder.dll => 0x7a16417d => 190
	i32 2055257422, ; 344: Xamarin.AndroidX.Lifecycle.LiveData.Core.dll => 0x7a80bd4e => 267
	i32 2060060697, ; 345: System.Windows.dll => 0x7aca0819 => 154
	i32 2066184531, ; 346: de\Microsoft.Maui.Controls.resources => 0x7b277953 => 316
	i32 2070888862, ; 347: System.Diagnostics.TraceSource => 0x7b6f419e => 33
	i32 2072397586, ; 348: Microsoft.Extensions.FileProviders.Physical => 0x7b864712 => 202
	i32 2079903147, ; 349: System.Runtime.dll => 0x7bf8cdab => 116
	i32 2090596640, ; 350: System.Numerics.Vectors => 0x7c9bf920 => 82
	i32 2103459038, ; 351: SQLitePCLRaw.provider.e_sqlite3.dll => 0x7d603cde => 227
	i32 2127167465, ; 352: System.Console => 0x7ec9ffe9 => 20
	i32 2142473426, ; 353: System.Collections.Specialized => 0x7fb38cd2 => 11
	i32 2143790110, ; 354: System.Xml.XmlSerializer.dll => 0x7fc7a41e => 162
	i32 2146852085, ; 355: Microsoft.VisualBasic.dll => 0x7ff65cf5 => 3
	i32 2159891885, ; 356: Microsoft.Maui => 0x80bd55ad => 215
	i32 2169148018, ; 357: hu\Microsoft.Maui.Controls.resources => 0x814a9272 => 324
	i32 2171397733, ; 358: Serilog.Sinks.Console.dll => 0x816ce665 => 221
	i32 2181485124, ; 359: Serilog.Sinks.File => 0x8206d244 => 222
	i32 2181898931, ; 360: Microsoft.Extensions.Options.dll => 0x820d22b3 => 209
	i32 2192057212, ; 361: Microsoft.Extensions.Logging.Abstractions => 0x82a8237c => 207
	i32 2193016926, ; 362: System.ObjectModel.dll => 0x82b6c85e => 84
	i32 2197979891, ; 363: Microsoft.Extensions.DependencyModel.dll => 0x830282f3 => 195
	i32 2201107256, ; 364: Xamarin.KotlinX.Coroutines.Core.Jvm.dll => 0x83323b38 => 311
	i32 2201231467, ; 365: System.Net.Http => 0x8334206b => 64
	i32 2207618523, ; 366: it\Microsoft.Maui.Controls.resources => 0x839595db => 326
	i32 2217644978, ; 367: Xamarin.AndroidX.VectorDrawable.Animated.dll => 0x842e93b2 => 294
	i32 2222056684, ; 368: System.Threading.Tasks.Parallel => 0x8471e4ec => 143
	i32 2229158877, ; 369: Microsoft.Extensions.Features.dll => 0x84de43dd => 200
	i32 2244775296, ; 370: Xamarin.AndroidX.LocalBroadcastManager => 0x85cc8d80 => 276
	i32 2252106437, ; 371: System.Xml.Serialization.dll => 0x863c6ac5 => 157
	i32 2252897993, ; 372: Microsoft.EntityFrameworkCore => 0x86487ec9 => 182
	i32 2256313426, ; 373: System.Globalization.Extensions => 0x867c9c52 => 41
	i32 2265110946, ; 374: System.Security.AccessControl.dll => 0x8702d9a2 => 117
	i32 2266799131, ; 375: Microsoft.Extensions.Configuration.Abstractions => 0x871c9c1b => 189
	i32 2267999099, ; 376: Xamarin.Android.Glide.DiskLruCache.dll => 0x872eeb7b => 231
	i32 2270573516, ; 377: fr/Microsoft.Maui.Controls.resources.dll => 0x875633cc => 320
	i32 2279755925, ; 378: Xamarin.AndroidX.RecyclerView.dll => 0x87e25095 => 283
	i32 2293034957, ; 379: System.ServiceModel.Web.dll => 0x88acefcd => 131
	i32 2295906218, ; 380: System.Net.Sockets => 0x88d8bfaa => 75
	i32 2298471582, ; 381: System.Net.Mail => 0x88ffe49e => 66
	i32 2303942373, ; 382: nb\Microsoft.Maui.Controls.resources => 0x89535ee5 => 330
	i32 2305521784, ; 383: System.Private.CoreLib.dll => 0x896b7878 => 172
	i32 2315684594, ; 384: Xamarin.AndroidX.Annotation.dll => 0x8a068af2 => 235
	i32 2319144366, ; 385: Microsoft.AspNetCore.SignalR.Client => 0x8a3b55ae => 177
	i32 2320631194, ; 386: System.Threading.Tasks.Parallel.dll => 0x8a52059a => 143
	i32 2340441535, ; 387: System.Runtime.InteropServices.RuntimeInformation.dll => 0x8b804dbf => 106
	i32 2344264397, ; 388: System.ValueTuple => 0x8bbaa2cd => 151
	i32 2353062107, ; 389: System.Net.Primitives => 0x8c40e0db => 70
	i32 2358249420, ; 390: Serilog.Extensions.Logging => 0x8c9007cc => 220
	i32 2368005991, ; 391: System.Xml.ReaderWriter.dll => 0x8d24e767 => 156
	i32 2371007202, ; 392: Microsoft.Extensions.Configuration => 0x8d52b2e2 => 188
	i32 2378619854, ; 393: System.Security.Cryptography.Csp.dll => 0x8dc6dbce => 121
	i32 2383496789, ; 394: System.Security.Principal.Windows.dll => 0x8e114655 => 127
	i32 2395872292, ; 395: id\Microsoft.Maui.Controls.resources => 0x8ece1c24 => 325
	i32 2401565422, ; 396: System.Web.HttpUtility => 0x8f24faee => 152
	i32 2403452196, ; 397: Xamarin.AndroidX.Emoji2.dll => 0x8f41c524 => 258
	i32 2421380589, ; 398: System.Threading.Tasks.Dataflow => 0x905355ed => 141
	i32 2423080555, ; 399: Xamarin.AndroidX.Collection.Ktx.dll => 0x906d466b => 245
	i32 2427813419, ; 400: hi\Microsoft.Maui.Controls.resources => 0x90b57e2b => 322
	i32 2435356389, ; 401: System.Console.dll => 0x912896e5 => 20
	i32 2435904999, ; 402: System.ComponentModel.DataAnnotations.dll => 0x9130f5e7 => 14
	i32 2454642406, ; 403: System.Text.Encoding.dll => 0x924edee6 => 135
	i32 2458678730, ; 404: System.Net.Sockets.dll => 0x928c75ca => 75
	i32 2459001652, ; 405: System.Linq.Parallel.dll => 0x92916334 => 59
	i32 2465273461, ; 406: SQLitePCLRaw.batteries_v2.dll => 0x92f11675 => 224
	i32 2465532216, ; 407: Xamarin.AndroidX.ConstraintLayout.Core.dll => 0x92f50938 => 248
	i32 2471841756, ; 408: netstandard.dll => 0x93554fdc => 167
	i32 2475788418, ; 409: Java.Interop.dll => 0x93918882 => 168
	i32 2480646305, ; 410: Microsoft.Maui.Controls => 0x93dba8a1 => 213
	i32 2483903535, ; 411: System.ComponentModel.EventBasedAsync => 0x940d5c2f => 15
	i32 2484371297, ; 412: System.Net.ServicePoint => 0x94147f61 => 74
	i32 2490993605, ; 413: System.AppContext.dll => 0x94798bc5 => 6
	i32 2501346920, ; 414: System.Data.DataSetExtensions => 0x95178668 => 23
	i32 2505896520, ; 415: Xamarin.AndroidX.Lifecycle.Runtime.dll => 0x955cf248 => 270
	i32 2522472828, ; 416: Xamarin.Android.Glide.dll => 0x9659e17c => 229
	i32 2538310050, ; 417: System.Reflection.Emit.Lightweight.dll => 0x974b89a2 => 91
	i32 2550873716, ; 418: hr\Microsoft.Maui.Controls.resources => 0x980b3e74 => 323
	i32 2562349572, ; 419: Microsoft.CSharp => 0x98ba5a04 => 1
	i32 2570120770, ; 420: System.Text.Encodings.Web => 0x9930ee42 => 136
	i32 2581783588, ; 421: Xamarin.AndroidX.Lifecycle.Runtime.Ktx => 0x99e2e424 => 271
	i32 2581819634, ; 422: Xamarin.AndroidX.VectorDrawable.dll => 0x99e370f2 => 293
	i32 2585220780, ; 423: System.Text.Encoding.Extensions.dll => 0x9a1756ac => 134
	i32 2585805581, ; 424: System.Net.Ping => 0x9a20430d => 69
	i32 2589602615, ; 425: System.Threading.ThreadPool => 0x9a5a3337 => 146
	i32 2592341985, ; 426: Microsoft.Extensions.FileProviders.Abstractions => 0x9a83ffe1 => 201
	i32 2593496499, ; 427: pl\Microsoft.Maui.Controls.resources => 0x9a959db3 => 332
	i32 2605712449, ; 428: Xamarin.KotlinX.Coroutines.Core.Jvm => 0x9b500441 => 311
	i32 2615233544, ; 429: Xamarin.AndroidX.Fragment.Ktx => 0x9be14c08 => 262
	i32 2616218305, ; 430: Microsoft.Extensions.Logging.Debug.dll => 0x9bf052c1 => 208
	i32 2617129537, ; 431: System.Private.Xml.dll => 0x9bfe3a41 => 88
	i32 2618712057, ; 432: System.Reflection.TypeExtensions.dll => 0x9c165ff9 => 96
	i32 2620871830, ; 433: Xamarin.AndroidX.CursorAdapter.dll => 0x9c375496 => 252
	i32 2624644809, ; 434: Xamarin.AndroidX.DynamicAnimation => 0x9c70e6c9 => 257
	i32 2626831493, ; 435: ja\Microsoft.Maui.Controls.resources => 0x9c924485 => 327
	i32 2627185994, ; 436: System.Diagnostics.TextWriterTraceListener.dll => 0x9c97ad4a => 31
	i32 2627802292, ; 437: Serilog.Extensions.Logging.dll => 0x9ca114b4 => 220
	i32 2629843544, ; 438: System.IO.Compression.ZipFile.dll => 0x9cc03a58 => 45
	i32 2633051222, ; 439: Xamarin.AndroidX.Lifecycle.LiveData => 0x9cf12c56 => 266
	i32 2634653062, ; 440: Microsoft.EntityFrameworkCore.Relational.dll => 0x9d099d86 => 184
	i32 2637500010, ; 441: Microsoft.Extensions.Features => 0x9d350e6a => 200
	i32 2663391936, ; 442: Xamarin.Android.Glide.DiskLruCache => 0x9ec022c0 => 231
	i32 2663698177, ; 443: System.Runtime.Loader => 0x9ec4cf01 => 109
	i32 2664396074, ; 444: System.Xml.XDocument.dll => 0x9ecf752a => 158
	i32 2665622720, ; 445: System.Drawing.Primitives => 0x9ee22cc0 => 35
	i32 2676780864, ; 446: System.Data.Common.dll => 0x9f8c6f40 => 22
	i32 2686887180, ; 447: System.Runtime.Serialization.Xml.dll => 0xa026a50c => 114
	i32 2693849962, ; 448: System.IO.dll => 0xa090e36a => 57
	i32 2701096212, ; 449: Xamarin.AndroidX.Tracing.Tracing => 0xa0ff7514 => 291
	i32 2715334215, ; 450: System.Threading.Tasks.dll => 0xa1d8b647 => 144
	i32 2717744543, ; 451: System.Security.Claims => 0xa1fd7d9f => 118
	i32 2719963679, ; 452: System.Security.Cryptography.Cng.dll => 0xa21f5a1f => 120
	i32 2724373263, ; 453: System.Runtime.Numerics.dll => 0xa262a30f => 110
	i32 2732626843, ; 454: Xamarin.AndroidX.Activity => 0xa2e0939b => 233
	i32 2735172069, ; 455: System.Threading.Channels => 0xa30769e5 => 139
	i32 2737747696, ; 456: Xamarin.AndroidX.AppCompat.AppCompatResources.dll => 0xa32eb6f0 => 239
	i32 2740948882, ; 457: System.IO.Pipes.AccessControl => 0xa35f8f92 => 54
	i32 2748088231, ; 458: System.Runtime.InteropServices.JavaScript => 0xa3cc7fa7 => 105
	i32 2752995522, ; 459: pt-BR\Microsoft.Maui.Controls.resources => 0xa41760c2 => 333
	i32 2758225723, ; 460: Microsoft.Maui.Controls.Xaml => 0xa4672f3b => 214
	i32 2764765095, ; 461: Microsoft.Maui.dll => 0xa4caf7a7 => 215
	i32 2765824710, ; 462: System.Text.Encoding.CodePages.dll => 0xa4db22c6 => 133
	i32 2770495804, ; 463: Xamarin.Jetbrains.Annotations.dll => 0xa522693c => 305
	i32 2778768386, ; 464: Xamarin.AndroidX.ViewPager.dll => 0xa5a0a402 => 296
	i32 2779977773, ; 465: Xamarin.AndroidX.ResourceInspection.Annotation.dll => 0xa5b3182d => 284
	i32 2785988530, ; 466: th\Microsoft.Maui.Controls.resources => 0xa60ecfb2 => 339
	i32 2788224221, ; 467: Xamarin.AndroidX.Fragment.Ktx.dll => 0xa630ecdd => 262
	i32 2801831435, ; 468: Microsoft.Maui.Graphics => 0xa7008e0b => 217
	i32 2803228030, ; 469: System.Xml.XPath.XDocument.dll => 0xa715dd7e => 159
	i32 2806116107, ; 470: es/Microsoft.Maui.Controls.resources.dll => 0xa741ef0b => 318
	i32 2810250172, ; 471: Xamarin.AndroidX.CoordinatorLayout.dll => 0xa78103bc => 249
	i32 2819470561, ; 472: System.Xml.dll => 0xa80db4e1 => 163
	i32 2821205001, ; 473: System.ServiceProcess.dll => 0xa8282c09 => 132
	i32 2821294376, ; 474: Xamarin.AndroidX.ResourceInspection.Annotation => 0xa8298928 => 284
	i32 2824502124, ; 475: System.Xml.XmlDocument => 0xa85a7b6c => 161
	i32 2831556043, ; 476: nl/Microsoft.Maui.Controls.resources.dll => 0xa8c61dcb => 331
	i32 2838993487, ; 477: Xamarin.AndroidX.Lifecycle.ViewModel.Ktx.dll => 0xa9379a4f => 273
	i32 2847789619, ; 478: Microsoft.EntityFrameworkCore.Relational => 0xa9bdd233 => 184
	i32 2849599387, ; 479: System.Threading.Overlapped.dll => 0xa9d96f9b => 140
	i32 2853208004, ; 480: Xamarin.AndroidX.ViewPager => 0xaa107fc4 => 296
	i32 2855708567, ; 481: Xamarin.AndroidX.Transition => 0xaa36a797 => 292
	i32 2861098320, ; 482: Mono.Android.Export.dll => 0xaa88e550 => 169
	i32 2861189240, ; 483: Microsoft.Maui.Essentials => 0xaa8a4878 => 216
	i32 2866463292, ; 484: Serilog.Sinks.Seq => 0xaadac23c => 223
	i32 2870099610, ; 485: Xamarin.AndroidX.Activity.Ktx.dll => 0xab123e9a => 234
	i32 2875164099, ; 486: Jsr305Binding.dll => 0xab5f85c3 => 301
	i32 2875220617, ; 487: System.Globalization.Calendars.dll => 0xab606289 => 40
	i32 2875347124, ; 488: Microsoft.AspNetCore.Http.Connections.Client.dll => 0xab6250b4 => 175
	i32 2884993177, ; 489: Xamarin.AndroidX.ExifInterface => 0xabf58099 => 260
	i32 2887636118, ; 490: System.Net.dll => 0xac1dd496 => 81
	i32 2899753641, ; 491: System.IO.UnmanagedMemoryStream => 0xacd6baa9 => 56
	i32 2900621748, ; 492: System.Dynamic.Runtime.dll => 0xace3f9b4 => 37
	i32 2901442782, ; 493: System.Reflection => 0xacf080de => 97
	i32 2905242038, ; 494: mscorlib.dll => 0xad2a79b6 => 166
	i32 2909740682, ; 495: System.Private.CoreLib => 0xad6f1e8a => 172
	i32 2911054922, ; 496: Microsoft.Extensions.FileProviders.Physical.dll => 0xad832c4a => 202
	i32 2916838712, ; 497: Xamarin.AndroidX.ViewPager2.dll => 0xaddb6d38 => 297
	i32 2919462931, ; 498: System.Numerics.Vectors.dll => 0xae037813 => 82
	i32 2921128767, ; 499: Xamarin.AndroidX.Annotation.Experimental.dll => 0xae1ce33f => 236
	i32 2936416060, ; 500: System.Resources.Reader => 0xaf06273c => 98
	i32 2940926066, ; 501: System.Diagnostics.StackTrace.dll => 0xaf4af872 => 30
	i32 2941540550, ; 502: VanAn.Shared.dll => 0xaf5458c6 => 346
	i32 2942453041, ; 503: System.Xml.XPath.XDocument => 0xaf624531 => 159
	i32 2959614098, ; 504: System.ComponentModel.dll => 0xb0682092 => 18
	i32 2968338931, ; 505: System.Security.Principal.Windows => 0xb0ed41f3 => 127
	i32 2971004615, ; 506: Microsoft.Extensions.Options.ConfigurationExtensions.dll => 0xb115eec7 => 210
	i32 2972252294, ; 507: System.Security.Cryptography.Algorithms.dll => 0xb128f886 => 119
	i32 2978675010, ; 508: Xamarin.AndroidX.DrawerLayout => 0xb18af942 => 256
	i32 2987532451, ; 509: Xamarin.AndroidX.Security.SecurityCrypto => 0xb21220a3 => 287
	i32 2996846495, ; 510: Xamarin.AndroidX.Lifecycle.Process.dll => 0xb2a03f9f => 269
	i32 3011185199, ; 511: Serilog.Extensions.Hosting => 0xb37b0a2f => 219
	i32 3016983068, ; 512: Xamarin.AndroidX.Startup.StartupRuntime => 0xb3d3821c => 289
	i32 3020703001, ; 513: Microsoft.Extensions.Diagnostics => 0xb40c4519 => 196
	i32 3023353419, ; 514: WindowsBase.dll => 0xb434b64b => 165
	i32 3024354802, ; 515: Xamarin.AndroidX.Legacy.Support.Core.Utils => 0xb443fdf2 => 264
	i32 3038032645, ; 516: _Microsoft.Android.Resource.Designer.dll => 0xb514b305 => 347
	i32 3038446544, ; 517: VanAn.Shared => 0xb51b03d0 => 346
	i32 3056245963, ; 518: Xamarin.AndroidX.SavedState.SavedState.Ktx => 0xb62a9ccb => 286
	i32 3057625584, ; 519: Xamarin.AndroidX.Navigation.Common => 0xb63fa9f0 => 277
	i32 3059408633, ; 520: Mono.Android.Runtime => 0xb65adef9 => 170
	i32 3059793426, ; 521: System.ComponentModel.Primitives => 0xb660be12 => 16
	i32 3069363400, ; 522: Microsoft.Extensions.Caching.Abstractions.dll => 0xb6f2c4c8 => 186
	i32 3075834255, ; 523: System.Threading.Tasks => 0xb755818f => 144
	i32 3077302341, ; 524: hu/Microsoft.Maui.Controls.resources.dll => 0xb76be845 => 324
	i32 3090735792, ; 525: System.Security.Cryptography.X509Certificates.dll => 0xb838e2b0 => 125
	i32 3099732863, ; 526: System.Security.Claims.dll => 0xb8c22b7f => 118
	i32 3103600923, ; 527: System.Formats.Asn1 => 0xb8fd311b => 38
	i32 3111772706, ; 528: System.Runtime.Serialization => 0xb979e222 => 115
	i32 3121463068, ; 529: System.IO.FileSystem.AccessControl.dll => 0xba0dbf1c => 47
	i32 3124832203, ; 530: System.Threading.Tasks.Extensions => 0xba4127cb => 142
	i32 3132293585, ; 531: System.Security.AccessControl => 0xbab301d1 => 117
	i32 3147165239, ; 532: System.Diagnostics.Tracing.dll => 0xbb95ee37 => 34
	i32 3148237826, ; 533: GoogleGson.dll => 0xbba64c02 => 173
	i32 3159123045, ; 534: System.Reflection.Primitives.dll => 0xbc4c6465 => 95
	i32 3160747431, ; 535: System.IO.MemoryMappedFiles => 0xbc652da7 => 53
	i32 3178803400, ; 536: Xamarin.AndroidX.Navigation.Fragment.dll => 0xbd78b0c8 => 278
	i32 3192346100, ; 537: System.Security.SecureString => 0xbe4755f4 => 129
	i32 3193515020, ; 538: System.Web => 0xbe592c0c => 153
	i32 3195844289, ; 539: Microsoft.Extensions.Caching.Abstractions => 0xbe7cb6c1 => 186
	i32 3204380047, ; 540: System.Data.dll => 0xbefef58f => 24
	i32 3209718065, ; 541: System.Xml.XmlDocument.dll => 0xbf506931 => 161
	i32 3211777861, ; 542: Xamarin.AndroidX.DocumentFile => 0xbf6fd745 => 255
	i32 3220365878, ; 543: System.Threading => 0xbff2e236 => 148
	i32 3226221578, ; 544: System.Runtime.Handles.dll => 0xc04c3c0a => 104
	i32 3251039220, ; 545: System.Reflection.DispatchProxy.dll => 0xc1c6ebf4 => 89
	i32 3258312781, ; 546: Xamarin.AndroidX.CardView => 0xc235e84d => 243
	i32 3265493905, ; 547: System.Linq.Queryable.dll => 0xc2a37b91 => 60
	i32 3265893370, ; 548: System.Threading.Tasks.Extensions.dll => 0xc2a993fa => 142
	i32 3277815716, ; 549: System.Resources.Writer.dll => 0xc35f7fa4 => 100
	i32 3279906254, ; 550: Microsoft.Win32.Registry.dll => 0xc37f65ce => 5
	i32 3280506390, ; 551: System.ComponentModel.Annotations.dll => 0xc3888e16 => 13
	i32 3290767353, ; 552: System.Security.Cryptography.Encoding => 0xc4251ff9 => 122
	i32 3299363146, ; 553: System.Text.Encoding => 0xc4a8494a => 135
	i32 3303498502, ; 554: System.Diagnostics.FileVersionInfo => 0xc4e76306 => 28
	i32 3305363605, ; 555: fi\Microsoft.Maui.Controls.resources => 0xc503d895 => 319
	i32 3316684772, ; 556: System.Net.Requests.dll => 0xc5b097e4 => 72
	i32 3317135071, ; 557: Xamarin.AndroidX.CustomView.dll => 0xc5b776df => 253
	i32 3317144872, ; 558: System.Data => 0xc5b79d28 => 24
	i32 3340431453, ; 559: Xamarin.AndroidX.Arch.Core.Runtime => 0xc71af05d => 241
	i32 3345895724, ; 560: Xamarin.AndroidX.ProfileInstaller.ProfileInstaller.dll => 0xc76e512c => 282
	i32 3346324047, ; 561: Xamarin.AndroidX.Navigation.Runtime => 0xc774da4f => 279
	i32 3357674450, ; 562: ru\Microsoft.Maui.Controls.resources => 0xc8220bd2 => 336
	i32 3358260929, ; 563: System.Text.Json => 0xc82afec1 => 137
	i32 3360279109, ; 564: SQLitePCLRaw.core => 0xc849ca45 => 225
	i32 3362336904, ; 565: Xamarin.AndroidX.Activity.Ktx => 0xc8693088 => 234
	i32 3362522851, ; 566: Xamarin.AndroidX.Core => 0xc86c06e3 => 250
	i32 3366347497, ; 567: Java.Interop => 0xc8a662e9 => 168
	i32 3374999561, ; 568: Xamarin.AndroidX.RecyclerView => 0xc92a6809 => 283
	i32 3381016424, ; 569: da\Microsoft.Maui.Controls.resources => 0xc9863768 => 315
	i32 3395150330, ; 570: System.Runtime.CompilerServices.Unsafe.dll => 0xca5de1fa => 101
	i32 3403906625, ; 571: System.Security.Cryptography.OpenSsl.dll => 0xcae37e41 => 123
	i32 3405233483, ; 572: Xamarin.AndroidX.CustomView.PoolingContainer => 0xcaf7bd4b => 254
	i32 3421170118, ; 573: Microsoft.Extensions.Configuration.Binder => 0xcbeae9c6 => 190
	i32 3428513518, ; 574: Microsoft.Extensions.DependencyInjection.dll => 0xcc5af6ee => 193
	i32 3429136800, ; 575: System.Xml => 0xcc6479a0 => 163
	i32 3430777524, ; 576: netstandard => 0xcc7d82b4 => 167
	i32 3441283291, ; 577: Xamarin.AndroidX.DynamicAnimation.dll => 0xcd1dd0db => 257
	i32 3445260447, ; 578: System.Formats.Tar => 0xcd5a809f => 39
	i32 3452344032, ; 579: Microsoft.Maui.Controls.Compatibility.dll => 0xcdc696e0 => 212
	i32 3463511458, ; 580: hr/Microsoft.Maui.Controls.resources.dll => 0xce70fda2 => 323
	i32 3466904072, ; 581: Microsoft.AspNetCore.SignalR.Client.dll => 0xcea4c208 => 177
	i32 3471940407, ; 582: System.ComponentModel.TypeConverter.dll => 0xcef19b37 => 17
	i32 3476120550, ; 583: Mono.Android => 0xcf3163e6 => 171
	i32 3479583265, ; 584: ru/Microsoft.Maui.Controls.resources.dll => 0xcf663a21 => 336
	i32 3484440000, ; 585: ro\Microsoft.Maui.Controls.resources => 0xcfb055c0 => 335
	i32 3485117614, ; 586: System.Text.Json.dll => 0xcfbaacae => 137
	i32 3486566296, ; 587: System.Transactions => 0xcfd0c798 => 150
	i32 3493954962, ; 588: Xamarin.AndroidX.Concurrent.Futures.dll => 0xd0418592 => 246
	i32 3509114376, ; 589: System.Xml.Linq => 0xd128d608 => 155
	i32 3515174580, ; 590: System.Security.dll => 0xd1854eb4 => 130
	i32 3530912306, ; 591: System.Configuration => 0xd2757232 => 19
	i32 3539954161, ; 592: System.Net.HttpListener => 0xd2ff69f1 => 65
	i32 3560100363, ; 593: System.Threading.Timer => 0xd432d20b => 147
	i32 3570554715, ; 594: System.IO.FileSystem.AccessControl => 0xd4d2575b => 47
	i32 3580758918, ; 595: zh-HK\Microsoft.Maui.Controls.resources => 0xd56e0b86 => 343
	i32 3597029428, ; 596: Xamarin.Android.Glide.GifDecoder.dll => 0xd6665034 => 232
	i32 3598340787, ; 597: System.Net.WebSockets.Client => 0xd67a52b3 => 79
	i32 3608519521, ; 598: System.Linq.dll => 0xd715a361 => 61
	i32 3624195450, ; 599: System.Runtime.InteropServices.RuntimeInformation => 0xd804d57a => 106
	i32 3627220390, ; 600: Xamarin.AndroidX.Print.dll => 0xd832fda6 => 281
	i32 3633644679, ; 601: Xamarin.AndroidX.Annotation.Experimental => 0xd8950487 => 236
	i32 3638274909, ; 602: System.IO.FileSystem.Primitives.dll => 0xd8dbab5d => 49
	i32 3641597786, ; 603: Xamarin.AndroidX.Lifecycle.LiveData.Core => 0xd90e5f5a => 267
	i32 3643446276, ; 604: tr\Microsoft.Maui.Controls.resources => 0xd92a9404 => 340
	i32 3643854240, ; 605: Xamarin.AndroidX.Navigation.Fragment => 0xd930cda0 => 278
	i32 3645089577, ; 606: System.ComponentModel.DataAnnotations => 0xd943a729 => 14
	i32 3657292374, ; 607: Microsoft.Extensions.Configuration.Abstractions.dll => 0xd9fdda56 => 189
	i32 3660523487, ; 608: System.Net.NetworkInformation => 0xda2f27df => 68
	i32 3672681054, ; 609: Mono.Android.dll => 0xdae8aa5e => 171
	i32 3682565725, ; 610: Xamarin.AndroidX.Browser => 0xdb7f7e5d => 242
	i32 3684561358, ; 611: Xamarin.AndroidX.Concurrent.Futures => 0xdb9df1ce => 246
	i32 3691870036, ; 612: Microsoft.AspNetCore.SignalR.Protocols.Json => 0xdc0d7754 => 180
	i32 3697841164, ; 613: zh-Hant/Microsoft.Maui.Controls.resources.dll => 0xdc68940c => 345
	i32 3700866549, ; 614: System.Net.WebProxy.dll => 0xdc96bdf5 => 78
	i32 3706696989, ; 615: Xamarin.AndroidX.Core.Core.Ktx.dll => 0xdcefb51d => 251
	i32 3716563718, ; 616: System.Runtime.Intrinsics => 0xdd864306 => 108
	i32 3718780102, ; 617: Xamarin.AndroidX.Annotation => 0xdda814c6 => 235
	i32 3722202641, ; 618: Microsoft.Extensions.Configuration.Json.dll => 0xdddc4e11 => 192
	i32 3724971120, ; 619: Xamarin.AndroidX.Navigation.Common.dll => 0xde068c70 => 277
	i32 3732100267, ; 620: System.Net.NameResolution => 0xde7354ab => 67
	i32 3737834244, ; 621: System.Net.Http.Json.dll => 0xdecad304 => 63
	i32 3748608112, ; 622: System.Diagnostics.DiagnosticSource => 0xdf6f3870 => 27
	i32 3751444290, ; 623: System.Xml.XPath => 0xdf9a7f42 => 160
	i32 3754567612, ; 624: SQLitePCLRaw.provider.e_sqlite3 => 0xdfca27bc => 227
	i32 3758424670, ; 625: Microsoft.Extensions.Configuration.FileExtensions => 0xe005025e => 191
	i32 3786282454, ; 626: Xamarin.AndroidX.Collection => 0xe1ae15d6 => 244
	i32 3787005001, ; 627: Microsoft.AspNetCore.Connections.Abstractions => 0xe1b91c49 => 174
	i32 3792276235, ; 628: System.Collections.NonGeneric => 0xe2098b0b => 10
	i32 3800979733, ; 629: Microsoft.Maui.Controls.Compatibility => 0xe28e5915 => 212
	i32 3802395368, ; 630: System.Collections.Specialized.dll => 0xe2a3f2e8 => 11
	i32 3819260425, ; 631: System.Net.WebProxy => 0xe3a54a09 => 78
	i32 3823082795, ; 632: System.Security.Cryptography.dll => 0xe3df9d2b => 126
	i32 3829621856, ; 633: System.Numerics.dll => 0xe4436460 => 83
	i32 3841636137, ; 634: Microsoft.Extensions.DependencyInjection.Abstractions.dll => 0xe4fab729 => 194
	i32 3844307129, ; 635: System.Net.Mail.dll => 0xe52378b9 => 66
	i32 3849253459, ; 636: System.Runtime.InteropServices.dll => 0xe56ef253 => 107
	i32 3870376305, ; 637: System.Net.HttpListener.dll => 0xe6b14171 => 65
	i32 3873536506, ; 638: System.Security.Principal => 0xe6e179fa => 128
	i32 3875112723, ; 639: System.Security.Cryptography.Encoding.dll => 0xe6f98713 => 122
	i32 3885497537, ; 640: System.Net.WebHeaderCollection.dll => 0xe797fcc1 => 77
	i32 3885922214, ; 641: Xamarin.AndroidX.Transition.dll => 0xe79e77a6 => 292
	i32 3888767677, ; 642: Xamarin.AndroidX.ProfileInstaller.ProfileInstaller => 0xe7c9e2bd => 282
	i32 3889960447, ; 643: zh-Hans/Microsoft.Maui.Controls.resources.dll => 0xe7dc15ff => 344
	i32 3896106733, ; 644: System.Collections.Concurrent.dll => 0xe839deed => 8
	i32 3896760992, ; 645: Xamarin.AndroidX.Core.dll => 0xe843daa0 => 250
	i32 3901907137, ; 646: Microsoft.VisualBasic.Core.dll => 0xe89260c1 => 2
	i32 3920810846, ; 647: System.IO.Compression.FileSystem.dll => 0xe9b2d35e => 44
	i32 3921031405, ; 648: Xamarin.AndroidX.VersionedParcelable.dll => 0xe9b630ed => 295
	i32 3928044579, ; 649: System.Xml.ReaderWriter => 0xea213423 => 156
	i32 3930554604, ; 650: System.Security.Principal.dll => 0xea4780ec => 128
	i32 3931092270, ; 651: Xamarin.AndroidX.Navigation.UI => 0xea4fb52e => 280
	i32 3945713374, ; 652: System.Data.DataSetExtensions.dll => 0xeb2ecede => 23
	i32 3953953790, ; 653: System.Text.Encoding.CodePages => 0xebac8bfe => 133
	i32 3955647286, ; 654: Xamarin.AndroidX.AppCompat.dll => 0xebc66336 => 238
	i32 3959773229, ; 655: Xamarin.AndroidX.Lifecycle.Process => 0xec05582d => 269
	i32 3971856949, ; 656: Microsoft.Extensions.Diagnostics.HealthChecks.dll => 0xecbdba35 => 198
	i32 3980434154, ; 657: th/Microsoft.Maui.Controls.resources.dll => 0xed409aea => 339
	i32 3987592930, ; 658: he/Microsoft.Maui.Controls.resources.dll => 0xedadd6e2 => 321
	i32 4003436829, ; 659: System.Diagnostics.Process.dll => 0xee9f991d => 29
	i32 4015948917, ; 660: Xamarin.AndroidX.Annotation.Jvm.dll => 0xef5e8475 => 237
	i32 4023392905, ; 661: System.IO.Pipelines => 0xefd01a89 => 228
	i32 4025784931, ; 662: System.Memory => 0xeff49a63 => 62
	i32 4046471985, ; 663: Microsoft.Maui.Controls.Xaml.dll => 0xf1304331 => 214
	i32 4054681211, ; 664: System.Reflection.Emit.ILGeneration => 0xf1ad867b => 90
	i32 4068434129, ; 665: System.Private.Xml.Linq.dll => 0xf27f60d1 => 87
	i32 4073602200, ; 666: System.Threading.dll => 0xf2ce3c98 => 148
	i32 4078967171, ; 667: Microsoft.Extensions.Hosting.Abstractions.dll => 0xf3201983 => 204
	i32 4094352644, ; 668: Microsoft.Maui.Essentials.dll => 0xf40add04 => 216
	i32 4099507663, ; 669: System.Drawing.dll => 0xf45985cf => 36
	i32 4100113165, ; 670: System.Private.Uri => 0xf462c30d => 86
	i32 4101593132, ; 671: Xamarin.AndroidX.Emoji2 => 0xf479582c => 258
	i32 4101842092, ; 672: Microsoft.Extensions.Caching.Memory => 0xf47d24ac => 187
	i32 4102112229, ; 673: pt/Microsoft.Maui.Controls.resources.dll => 0xf48143e5 => 334
	i32 4125707920, ; 674: ms/Microsoft.Maui.Controls.resources.dll => 0xf5e94e90 => 329
	i32 4126470640, ; 675: Microsoft.Extensions.DependencyInjection => 0xf5f4f1f0 => 193
	i32 4127667938, ; 676: System.IO.FileSystem.Watcher => 0xf60736e2 => 50
	i32 4130442656, ; 677: System.AppContext => 0xf6318da0 => 6
	i32 4147896353, ; 678: System.Reflection.Emit.ILGeneration.dll => 0xf73be021 => 90
	i32 4150914736, ; 679: uk\Microsoft.Maui.Controls.resources => 0xf769eeb0 => 341
	i32 4151237749, ; 680: System.Core => 0xf76edc75 => 21
	i32 4159265925, ; 681: System.Xml.XmlSerializer => 0xf7e95c85 => 162
	i32 4161255271, ; 682: System.Reflection.TypeExtensions => 0xf807b767 => 96
	i32 4164802419, ; 683: System.IO.FileSystem.Watcher.dll => 0xf83dd773 => 50
	i32 4181436372, ; 684: System.Runtime.Serialization.Primitives => 0xf93ba7d4 => 113
	i32 4182413190, ; 685: Xamarin.AndroidX.Lifecycle.ViewModelSavedState.dll => 0xf94a8f86 => 274
	i32 4185676441, ; 686: System.Security => 0xf97c5a99 => 130
	i32 4196529839, ; 687: System.Net.WebClient.dll => 0xfa21f6af => 76
	i32 4213026141, ; 688: System.Diagnostics.DiagnosticSource.dll => 0xfb1dad5d => 27
	i32 4256097574, ; 689: Xamarin.AndroidX.Core.Core.Ktx => 0xfdaee526 => 251
	i32 4258378803, ; 690: Xamarin.AndroidX.Lifecycle.ViewModel.Ktx => 0xfdd1b433 => 273
	i32 4260525087, ; 691: System.Buffers => 0xfdf2741f => 7
	i32 4271975918, ; 692: Microsoft.Maui.Controls.dll => 0xfea12dee => 213
	i32 4274976490, ; 693: System.Runtime.Numerics => 0xfecef6ea => 110
	i32 4292120959, ; 694: Xamarin.AndroidX.Lifecycle.ViewModelSavedState => 0xffd4917f => 274
	i32 4294763496 ; 695: Xamarin.AndroidX.ExifInterface.dll => 0xfffce3e8 => 260
], align 4

@assembly_image_cache_indices = dso_local local_unnamed_addr constant [696 x i32] [
	i32 68, ; 0
	i32 67, ; 1
	i32 108, ; 2
	i32 195, ; 3
	i32 270, ; 4
	i32 304, ; 5
	i32 48, ; 6
	i32 80, ; 7
	i32 145, ; 8
	i32 30, ; 9
	i32 345, ; 10
	i32 124, ; 11
	i32 217, ; 12
	i32 102, ; 13
	i32 197, ; 14
	i32 288, ; 15
	i32 107, ; 16
	i32 288, ; 17
	i32 139, ; 18
	i32 308, ; 19
	i32 77, ; 20
	i32 124, ; 21
	i32 13, ; 22
	i32 244, ; 23
	i32 132, ; 24
	i32 290, ; 25
	i32 151, ; 26
	i32 342, ; 27
	i32 343, ; 28
	i32 18, ; 29
	i32 242, ; 30
	i32 26, ; 31
	i32 175, ; 32
	i32 196, ; 33
	i32 264, ; 34
	i32 1, ; 35
	i32 59, ; 36
	i32 42, ; 37
	i32 91, ; 38
	i32 247, ; 39
	i32 147, ; 40
	i32 266, ; 41
	i32 263, ; 42
	i32 314, ; 43
	i32 54, ; 44
	i32 205, ; 45
	i32 69, ; 46
	i32 342, ; 47
	i32 233, ; 48
	i32 83, ; 49
	i32 327, ; 50
	i32 265, ; 51
	i32 226, ; 52
	i32 176, ; 53
	i32 326, ; 54
	i32 131, ; 55
	i32 55, ; 56
	i32 149, ; 57
	i32 74, ; 58
	i32 145, ; 59
	i32 198, ; 60
	i32 62, ; 61
	i32 219, ; 62
	i32 146, ; 63
	i32 347, ; 64
	i32 221, ; 65
	i32 165, ; 66
	i32 338, ; 67
	i32 248, ; 68
	i32 12, ; 69
	i32 261, ; 70
	i32 125, ; 71
	i32 152, ; 72
	i32 179, ; 73
	i32 113, ; 74
	i32 166, ; 75
	i32 164, ; 76
	i32 263, ; 77
	i32 276, ; 78
	i32 84, ; 79
	i32 325, ; 80
	i32 319, ; 81
	i32 211, ; 82
	i32 150, ; 83
	i32 308, ; 84
	i32 60, ; 85
	i32 206, ; 86
	i32 51, ; 87
	i32 103, ; 88
	i32 114, ; 89
	i32 40, ; 90
	i32 301, ; 91
	i32 199, ; 92
	i32 299, ; 93
	i32 120, ; 94
	i32 333, ; 95
	i32 52, ; 96
	i32 44, ; 97
	i32 119, ; 98
	i32 0, ; 99
	i32 253, ; 100
	i32 331, ; 101
	i32 199, ; 102
	i32 259, ; 103
	i32 81, ; 104
	i32 136, ; 105
	i32 295, ; 106
	i32 240, ; 107
	i32 8, ; 108
	i32 73, ; 109
	i32 313, ; 110
	i32 155, ; 111
	i32 310, ; 112
	i32 154, ; 113
	i32 92, ; 114
	i32 305, ; 115
	i32 45, ; 116
	i32 328, ; 117
	i32 316, ; 118
	i32 309, ; 119
	i32 109, ; 120
	i32 210, ; 121
	i32 129, ; 122
	i32 224, ; 123
	i32 25, ; 124
	i32 230, ; 125
	i32 72, ; 126
	i32 55, ; 127
	i32 46, ; 128
	i32 337, ; 129
	i32 209, ; 130
	i32 254, ; 131
	i32 22, ; 132
	i32 268, ; 133
	i32 218, ; 134
	i32 86, ; 135
	i32 43, ; 136
	i32 160, ; 137
	i32 180, ; 138
	i32 71, ; 139
	i32 281, ; 140
	i32 3, ; 141
	i32 42, ; 142
	i32 63, ; 143
	i32 16, ; 144
	i32 0, ; 145
	i32 53, ; 146
	i32 340, ; 147
	i32 304, ; 148
	i32 105, ; 149
	i32 309, ; 150
	i32 302, ; 151
	i32 265, ; 152
	i32 34, ; 153
	i32 158, ; 154
	i32 85, ; 155
	i32 32, ; 156
	i32 12, ; 157
	i32 51, ; 158
	i32 203, ; 159
	i32 56, ; 160
	i32 285, ; 161
	i32 36, ; 162
	i32 194, ; 163
	i32 315, ; 164
	i32 303, ; 165
	i32 238, ; 166
	i32 35, ; 167
	i32 58, ; 168
	i32 197, ; 169
	i32 272, ; 170
	i32 176, ; 171
	i32 173, ; 172
	i32 17, ; 173
	i32 306, ; 174
	i32 164, ; 175
	i32 191, ; 176
	i32 204, ; 177
	i32 328, ; 178
	i32 271, ; 179
	i32 208, ; 180
	i32 298, ; 181
	i32 183, ; 182
	i32 334, ; 183
	i32 153, ; 184
	i32 201, ; 185
	i32 294, ; 186
	i32 279, ; 187
	i32 183, ; 188
	i32 332, ; 189
	i32 240, ; 190
	i32 187, ; 191
	i32 29, ; 192
	i32 52, ; 193
	i32 178, ; 194
	i32 330, ; 195
	i32 299, ; 196
	i32 5, ; 197
	i32 314, ; 198
	i32 289, ; 199
	i32 293, ; 200
	i32 245, ; 201
	i32 310, ; 202
	i32 237, ; 203
	i32 225, ; 204
	i32 256, ; 205
	i32 85, ; 206
	i32 298, ; 207
	i32 222, ; 208
	i32 61, ; 209
	i32 112, ; 210
	i32 57, ; 211
	i32 344, ; 212
	i32 285, ; 213
	i32 99, ; 214
	i32 19, ; 215
	i32 249, ; 216
	i32 111, ; 217
	i32 101, ; 218
	i32 174, ; 219
	i32 102, ; 220
	i32 312, ; 221
	i32 104, ; 222
	i32 302, ; 223
	i32 71, ; 224
	i32 38, ; 225
	i32 32, ; 226
	i32 103, ; 227
	i32 73, ; 228
	i32 318, ; 229
	i32 9, ; 230
	i32 123, ; 231
	i32 46, ; 232
	i32 239, ; 233
	i32 211, ; 234
	i32 9, ; 235
	i32 43, ; 236
	i32 4, ; 237
	i32 286, ; 238
	i32 181, ; 239
	i32 322, ; 240
	i32 223, ; 241
	i32 205, ; 242
	i32 317, ; 243
	i32 203, ; 244
	i32 31, ; 245
	i32 138, ; 246
	i32 92, ; 247
	i32 93, ; 248
	i32 337, ; 249
	i32 49, ; 250
	i32 141, ; 251
	i32 112, ; 252
	i32 140, ; 253
	i32 255, ; 254
	i32 115, ; 255
	i32 303, ; 256
	i32 157, ; 257
	i32 76, ; 258
	i32 79, ; 259
	i32 275, ; 260
	i32 37, ; 261
	i32 297, ; 262
	i32 218, ; 263
	i32 192, ; 264
	i32 259, ; 265
	i32 252, ; 266
	i32 64, ; 267
	i32 138, ; 268
	i32 15, ; 269
	i32 116, ; 270
	i32 291, ; 271
	i32 300, ; 272
	i32 247, ; 273
	i32 48, ; 274
	i32 70, ; 275
	i32 80, ; 276
	i32 126, ; 277
	i32 181, ; 278
	i32 182, ; 279
	i32 94, ; 280
	i32 121, ; 281
	i32 307, ; 282
	i32 26, ; 283
	i32 226, ; 284
	i32 268, ; 285
	i32 97, ; 286
	i32 28, ; 287
	i32 243, ; 288
	i32 335, ; 289
	i32 313, ; 290
	i32 149, ; 291
	i32 228, ; 292
	i32 169, ; 293
	i32 4, ; 294
	i32 98, ; 295
	i32 33, ; 296
	i32 93, ; 297
	i32 290, ; 298
	i32 206, ; 299
	i32 21, ; 300
	i32 41, ; 301
	i32 170, ; 302
	i32 329, ; 303
	i32 261, ; 304
	i32 321, ; 305
	i32 275, ; 306
	i32 306, ; 307
	i32 300, ; 308
	i32 280, ; 309
	i32 2, ; 310
	i32 134, ; 311
	i32 111, ; 312
	i32 207, ; 313
	i32 341, ; 314
	i32 230, ; 315
	i32 338, ; 316
	i32 58, ; 317
	i32 95, ; 318
	i32 320, ; 319
	i32 39, ; 320
	i32 241, ; 321
	i32 185, ; 322
	i32 25, ; 323
	i32 94, ; 324
	i32 89, ; 325
	i32 99, ; 326
	i32 10, ; 327
	i32 87, ; 328
	i32 178, ; 329
	i32 100, ; 330
	i32 287, ; 331
	i32 179, ; 332
	i32 188, ; 333
	i32 307, ; 334
	i32 232, ; 335
	i32 317, ; 336
	i32 7, ; 337
	i32 185, ; 338
	i32 272, ; 339
	i32 312, ; 340
	i32 229, ; 341
	i32 88, ; 342
	i32 190, ; 343
	i32 267, ; 344
	i32 154, ; 345
	i32 316, ; 346
	i32 33, ; 347
	i32 202, ; 348
	i32 116, ; 349
	i32 82, ; 350
	i32 227, ; 351
	i32 20, ; 352
	i32 11, ; 353
	i32 162, ; 354
	i32 3, ; 355
	i32 215, ; 356
	i32 324, ; 357
	i32 221, ; 358
	i32 222, ; 359
	i32 209, ; 360
	i32 207, ; 361
	i32 84, ; 362
	i32 195, ; 363
	i32 311, ; 364
	i32 64, ; 365
	i32 326, ; 366
	i32 294, ; 367
	i32 143, ; 368
	i32 200, ; 369
	i32 276, ; 370
	i32 157, ; 371
	i32 182, ; 372
	i32 41, ; 373
	i32 117, ; 374
	i32 189, ; 375
	i32 231, ; 376
	i32 320, ; 377
	i32 283, ; 378
	i32 131, ; 379
	i32 75, ; 380
	i32 66, ; 381
	i32 330, ; 382
	i32 172, ; 383
	i32 235, ; 384
	i32 177, ; 385
	i32 143, ; 386
	i32 106, ; 387
	i32 151, ; 388
	i32 70, ; 389
	i32 220, ; 390
	i32 156, ; 391
	i32 188, ; 392
	i32 121, ; 393
	i32 127, ; 394
	i32 325, ; 395
	i32 152, ; 396
	i32 258, ; 397
	i32 141, ; 398
	i32 245, ; 399
	i32 322, ; 400
	i32 20, ; 401
	i32 14, ; 402
	i32 135, ; 403
	i32 75, ; 404
	i32 59, ; 405
	i32 224, ; 406
	i32 248, ; 407
	i32 167, ; 408
	i32 168, ; 409
	i32 213, ; 410
	i32 15, ; 411
	i32 74, ; 412
	i32 6, ; 413
	i32 23, ; 414
	i32 270, ; 415
	i32 229, ; 416
	i32 91, ; 417
	i32 323, ; 418
	i32 1, ; 419
	i32 136, ; 420
	i32 271, ; 421
	i32 293, ; 422
	i32 134, ; 423
	i32 69, ; 424
	i32 146, ; 425
	i32 201, ; 426
	i32 332, ; 427
	i32 311, ; 428
	i32 262, ; 429
	i32 208, ; 430
	i32 88, ; 431
	i32 96, ; 432
	i32 252, ; 433
	i32 257, ; 434
	i32 327, ; 435
	i32 31, ; 436
	i32 220, ; 437
	i32 45, ; 438
	i32 266, ; 439
	i32 184, ; 440
	i32 200, ; 441
	i32 231, ; 442
	i32 109, ; 443
	i32 158, ; 444
	i32 35, ; 445
	i32 22, ; 446
	i32 114, ; 447
	i32 57, ; 448
	i32 291, ; 449
	i32 144, ; 450
	i32 118, ; 451
	i32 120, ; 452
	i32 110, ; 453
	i32 233, ; 454
	i32 139, ; 455
	i32 239, ; 456
	i32 54, ; 457
	i32 105, ; 458
	i32 333, ; 459
	i32 214, ; 460
	i32 215, ; 461
	i32 133, ; 462
	i32 305, ; 463
	i32 296, ; 464
	i32 284, ; 465
	i32 339, ; 466
	i32 262, ; 467
	i32 217, ; 468
	i32 159, ; 469
	i32 318, ; 470
	i32 249, ; 471
	i32 163, ; 472
	i32 132, ; 473
	i32 284, ; 474
	i32 161, ; 475
	i32 331, ; 476
	i32 273, ; 477
	i32 184, ; 478
	i32 140, ; 479
	i32 296, ; 480
	i32 292, ; 481
	i32 169, ; 482
	i32 216, ; 483
	i32 223, ; 484
	i32 234, ; 485
	i32 301, ; 486
	i32 40, ; 487
	i32 175, ; 488
	i32 260, ; 489
	i32 81, ; 490
	i32 56, ; 491
	i32 37, ; 492
	i32 97, ; 493
	i32 166, ; 494
	i32 172, ; 495
	i32 202, ; 496
	i32 297, ; 497
	i32 82, ; 498
	i32 236, ; 499
	i32 98, ; 500
	i32 30, ; 501
	i32 346, ; 502
	i32 159, ; 503
	i32 18, ; 504
	i32 127, ; 505
	i32 210, ; 506
	i32 119, ; 507
	i32 256, ; 508
	i32 287, ; 509
	i32 269, ; 510
	i32 219, ; 511
	i32 289, ; 512
	i32 196, ; 513
	i32 165, ; 514
	i32 264, ; 515
	i32 347, ; 516
	i32 346, ; 517
	i32 286, ; 518
	i32 277, ; 519
	i32 170, ; 520
	i32 16, ; 521
	i32 186, ; 522
	i32 144, ; 523
	i32 324, ; 524
	i32 125, ; 525
	i32 118, ; 526
	i32 38, ; 527
	i32 115, ; 528
	i32 47, ; 529
	i32 142, ; 530
	i32 117, ; 531
	i32 34, ; 532
	i32 173, ; 533
	i32 95, ; 534
	i32 53, ; 535
	i32 278, ; 536
	i32 129, ; 537
	i32 153, ; 538
	i32 186, ; 539
	i32 24, ; 540
	i32 161, ; 541
	i32 255, ; 542
	i32 148, ; 543
	i32 104, ; 544
	i32 89, ; 545
	i32 243, ; 546
	i32 60, ; 547
	i32 142, ; 548
	i32 100, ; 549
	i32 5, ; 550
	i32 13, ; 551
	i32 122, ; 552
	i32 135, ; 553
	i32 28, ; 554
	i32 319, ; 555
	i32 72, ; 556
	i32 253, ; 557
	i32 24, ; 558
	i32 241, ; 559
	i32 282, ; 560
	i32 279, ; 561
	i32 336, ; 562
	i32 137, ; 563
	i32 225, ; 564
	i32 234, ; 565
	i32 250, ; 566
	i32 168, ; 567
	i32 283, ; 568
	i32 315, ; 569
	i32 101, ; 570
	i32 123, ; 571
	i32 254, ; 572
	i32 190, ; 573
	i32 193, ; 574
	i32 163, ; 575
	i32 167, ; 576
	i32 257, ; 577
	i32 39, ; 578
	i32 212, ; 579
	i32 323, ; 580
	i32 177, ; 581
	i32 17, ; 582
	i32 171, ; 583
	i32 336, ; 584
	i32 335, ; 585
	i32 137, ; 586
	i32 150, ; 587
	i32 246, ; 588
	i32 155, ; 589
	i32 130, ; 590
	i32 19, ; 591
	i32 65, ; 592
	i32 147, ; 593
	i32 47, ; 594
	i32 343, ; 595
	i32 232, ; 596
	i32 79, ; 597
	i32 61, ; 598
	i32 106, ; 599
	i32 281, ; 600
	i32 236, ; 601
	i32 49, ; 602
	i32 267, ; 603
	i32 340, ; 604
	i32 278, ; 605
	i32 14, ; 606
	i32 189, ; 607
	i32 68, ; 608
	i32 171, ; 609
	i32 242, ; 610
	i32 246, ; 611
	i32 180, ; 612
	i32 345, ; 613
	i32 78, ; 614
	i32 251, ; 615
	i32 108, ; 616
	i32 235, ; 617
	i32 192, ; 618
	i32 277, ; 619
	i32 67, ; 620
	i32 63, ; 621
	i32 27, ; 622
	i32 160, ; 623
	i32 227, ; 624
	i32 191, ; 625
	i32 244, ; 626
	i32 174, ; 627
	i32 10, ; 628
	i32 212, ; 629
	i32 11, ; 630
	i32 78, ; 631
	i32 126, ; 632
	i32 83, ; 633
	i32 194, ; 634
	i32 66, ; 635
	i32 107, ; 636
	i32 65, ; 637
	i32 128, ; 638
	i32 122, ; 639
	i32 77, ; 640
	i32 292, ; 641
	i32 282, ; 642
	i32 344, ; 643
	i32 8, ; 644
	i32 250, ; 645
	i32 2, ; 646
	i32 44, ; 647
	i32 295, ; 648
	i32 156, ; 649
	i32 128, ; 650
	i32 280, ; 651
	i32 23, ; 652
	i32 133, ; 653
	i32 238, ; 654
	i32 269, ; 655
	i32 198, ; 656
	i32 339, ; 657
	i32 321, ; 658
	i32 29, ; 659
	i32 237, ; 660
	i32 228, ; 661
	i32 62, ; 662
	i32 214, ; 663
	i32 90, ; 664
	i32 87, ; 665
	i32 148, ; 666
	i32 204, ; 667
	i32 216, ; 668
	i32 36, ; 669
	i32 86, ; 670
	i32 258, ; 671
	i32 187, ; 672
	i32 334, ; 673
	i32 329, ; 674
	i32 193, ; 675
	i32 50, ; 676
	i32 6, ; 677
	i32 90, ; 678
	i32 341, ; 679
	i32 21, ; 680
	i32 162, ; 681
	i32 96, ; 682
	i32 50, ; 683
	i32 113, ; 684
	i32 274, ; 685
	i32 130, ; 686
	i32 76, ; 687
	i32 27, ; 688
	i32 251, ; 689
	i32 273, ; 690
	i32 7, ; 691
	i32 213, ; 692
	i32 110, ; 693
	i32 274, ; 694
	i32 260 ; 695
], align 4

@marshal_methods_number_of_classes = dso_local local_unnamed_addr constant i32 0, align 4

@marshal_methods_class_cache = dso_local local_unnamed_addr global [0 x %struct.MarshalMethodsManagedClass] zeroinitializer, align 4

; Names of classes in which marshal methods reside
@mm_class_names = dso_local local_unnamed_addr constant [0 x ptr] zeroinitializer, align 4

@mm_method_names = dso_local local_unnamed_addr constant [1 x %struct.MarshalMethodName] [
	%struct.MarshalMethodName {
		i64 0, ; id 0x0; name: 
		ptr @.MarshalMethodName.0_name; char* name
	} ; 0
], align 8

; get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void*& target_ptr)
@get_function_pointer = internal dso_local unnamed_addr global ptr null, align 4

; Functions

; Function attributes: "min-legal-vector-width"="0" mustprogress "no-trapping-math"="true" nofree norecurse nosync nounwind "stack-protector-buffer-size"="8" uwtable willreturn
define void @xamarin_app_init(ptr nocapture noundef readnone %env, ptr noundef %fn) local_unnamed_addr #0
{
	%fnIsNull = icmp eq ptr %fn, null
	br i1 %fnIsNull, label %1, label %2

1: ; preds = %0
	%putsResult = call noundef i32 @puts(ptr @.str.0)
	call void @abort()
	unreachable 

2: ; preds = %1, %0
	store ptr %fn, ptr @get_function_pointer, align 4, !tbaa !3
	ret void
}

; Strings
@.str.0 = private unnamed_addr constant [40 x i8] c"get_function_pointer MUST be specified\0A\00", align 1

;MarshalMethodName
@.MarshalMethodName.0_name = private unnamed_addr constant [1 x i8] c"\00", align 1

; External functions

; Function attributes: "no-trapping-math"="true" noreturn nounwind "stack-protector-buffer-size"="8"
declare void @abort() local_unnamed_addr #2

; Function attributes: nofree nounwind
declare noundef i32 @puts(ptr noundef) local_unnamed_addr #1
attributes #0 = { "min-legal-vector-width"="0" mustprogress "no-trapping-math"="true" nofree norecurse nosync nounwind "stack-protector-buffer-size"="8" "stackrealign" "target-cpu"="i686" "target-features"="+cx8,+mmx,+sse,+sse2,+sse3,+ssse3,+x87" "tune-cpu"="generic" uwtable willreturn }
attributes #1 = { nofree nounwind }
attributes #2 = { "no-trapping-math"="true" noreturn nounwind "stack-protector-buffer-size"="8" "stackrealign" "target-cpu"="i686" "target-features"="+cx8,+mmx,+sse,+sse2,+sse3,+ssse3,+x87" "tune-cpu"="generic" }

; Metadata
!llvm.module.flags = !{!0, !1, !7}
!0 = !{i32 1, !"wchar_size", i32 4}
!1 = !{i32 7, !"PIC Level", i32 2}
!llvm.ident = !{!2}
!2 = !{!"Xamarin.Android remotes/origin/release/8.0.4xx @ 82d8938cf80f6d5fa6c28529ddfbdb753d805ab4"}
!3 = !{!4, !4, i64 0}
!4 = !{!"any pointer", !5, i64 0}
!5 = !{!"omnipotent char", !6, i64 0}
!6 = !{!"Simple C++ TBAA"}
!7 = !{i32 1, !"NumRegisterParameters", i32 0}
