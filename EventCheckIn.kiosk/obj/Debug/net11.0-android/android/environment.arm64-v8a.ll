source_filename = "environment.arm64-v8a.ll"
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android21"

%struct.AppEnvironmentVariable = type {
	i32, 
	i32 
}

%struct.ApplicationConfigCLR = type {
	i1, 
	i1, 
	i1, 
	i1, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	ptr, 
	i1 
}

%struct.AssemblyStoreAssemblyDescriptor = type {
	i32, 
	i32, 
	i32, 
	i32, 
	i32, 
	i32 
}

%struct.AssemblyStoreRuntimeData = type {
	ptr, 
	i32, 
	i32, 
	ptr 
}

%struct.AssemblyStoreSingleAssemblyRuntimeData = type {
	ptr, 
	ptr, 
	ptr, 
	ptr 
}

%struct.DSOCacheEntry = type {
	i32, 
	i1, 
	i1, 
	i32, 
	ptr 
}

%struct.XamarinAndroidBundledAssembly = type {
	i32, 
	ptr, 
	i32, 
	i32, 
	ptr, 
	i32, 
	ptr 
}

@format_tag = dso_local local_unnamed_addr constant i64 666756936985944, align 8

@app_environment_variables = dso_local local_unnamed_addr constant [2 x %struct.AppEnvironmentVariable] [
	%struct.AppEnvironmentVariable {
		i32 0, 
		i32 15
	},
	%struct.AppEnvironmentVariable {
		i32 36, 
		i32 51
	}
], align 4

@app_environment_variable_contents = dso_local local_unnamed_addr constant [56 x i8] c"MONO_GC_PARAMS\00major=marksweep-conc\00MONO_LOG_LEVEL\00info\00", align 1

@app_system_properties = dso_local local_unnamed_addr constant [0 x %struct.AppEnvironmentVariable] zeroinitializer, align 4

@app_system_property_contents = dso_local local_unnamed_addr constant [0 x i8] c"", align 1

@application_config = dso_local local_unnamed_addr constant %struct.ApplicationConfigCLR {
	i1 false, 
	i1 false, 
	i1 false, 
	i1 false, 
	i32 31, 
	i32 3, 
	i32 2, 
	i32 0, 
	i32 345, 
	i32 69, 
	i32 44, 
	i32 11, 
	i32 u0x02001497, 
	i32 u0x06018da1, 
	i32 u0x06018d9d, 
	i32 0, 
	i32 0, 
	ptr @.ApplicationConfigCLR.0_android_package_name, 
	i1 false
}, align 8

@dso_cache = dso_local local_unnamed_addr global [44 x %struct.DSOCacheEntry] [
	%struct.DSOCacheEntry {
		i32 u0x0387615c, 
		i1 false, 
		i1 false, 
		i32 194, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x04410531, 
		i1 false, 
		i1 false, 
		i32 194, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x0547c1a9, 
		i1 false, 
		i1 true, 
		i32 144, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x0a9877ba, 
		i1 false, 
		i1 false, 
		i32 124, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x271e76c2, 
		i1 false, 
		i1 false, 
		i32 240, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x2ecb3d2a, 
		i1 false, 
		i1 false, 
		i32 124, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x32742da4, 
		i1 false, 
		i1 false, 
		i32 221, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x3a5024d0, 
		i1 false, 
		i1 false, 
		i32 194, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x3b9b9a1f, 
		i1 false, 
		i1 false, 
		i32 23, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x3e2585ed, 
		i1 false, 
		i1 true, 
		i32 255, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x42ee6b8f, 
		i1 false, 
		i1 false, 
		i32 124, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x4709749a, 
		i1 false, 
		i1 false, 
		i32 124, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x4d88eee4, 
		i1 false, 
		i1 false, 
		i32 194, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x505199c8, 
		i1 false, 
		i1 false, 
		i32 0, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x50d7ea57, 
		i1 false, 
		i1 false, 
		i32 207, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x5af6f887, 
		i1 false, 
		i1 false, 
		i32 221, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x5fae2973, 
		i1 false, 
		i1 true, 
		i32 255, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x62a55a41, 
		i1 false, 
		i1 false, 
		i32 55, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x6bfed756, 
		i1 false, 
		i1 true, 
		i32 144, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x6ca8c1d5, 
		i1 false, 
		i1 false, 
		i32 207, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x702a0a55, 
		i1 false, 
		i1 true, 
		i32 255, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x760582cc, 
		i1 false, 
		i1 false, 
		i32 240, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x7d160aeb, 
		i1 false, 
		i1 false, 
		i32 55, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x800f0a0d, 
		i1 false, 
		i1 false, 
		i32 240, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x833de128, 
		i1 false, 
		i1 false, 
		i32 55, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x84285211, 
		i1 false, 
		i1 false, 
		i32 89, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x8b03e6ef, 
		i1 false, 
		i1 false, 
		i32 240, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x9523de66, 
		i1 false, 
		i1 false, 
		i32 207, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0x9da5e2a3, 
		i1 false, 
		i1 true, 
		i32 144, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xb234abb7, 
		i1 false, 
		i1 false, 
		i32 89, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xb451baeb, 
		i1 false, 
		i1 false, 
		i32 89, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xb8471f80, 
		i1 false, 
		i1 true, 
		i32 255, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xb8db9db4, 
		i1 false, 
		i1 false, 
		i32 89, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xb9888113, 
		i1 false, 
		i1 false, 
		i32 0, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xbb9b021a, 
		i1 false, 
		i1 false, 
		i32 207, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xbdbefe40, 
		i1 false, 
		i1 false, 
		i32 221, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xbf0186e3, 
		i1 false, 
		i1 false, 
		i32 55, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xd59d1b57, 
		i1 false, 
		i1 false, 
		i32 221, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xd998d188, 
		i1 false, 
		i1 false, 
		i32 23, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xe8c03aae, 
		i1 false, 
		i1 true, 
		i32 144, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xee9db78c, 
		i1 false, 
		i1 false, 
		i32 23, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xef84282e, 
		i1 false, 
		i1 false, 
		i32 23, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xf94ab5ef, 
		i1 false, 
		i1 false, 
		i32 0, 
		ptr null
	},
	%struct.DSOCacheEntry {
		i32 u0xfcadaafa, 
		i1 false, 
		i1 false, 
		i32 0, 
		ptr null
	}
], align 8

@dso_jni_preloads_idx_stride = dso_local local_unnamed_addr constant i32 4, align 4

@dso_jni_preloads_idx_count = dso_local local_unnamed_addr constant i64 4, align 8

@dso_jni_preloads_idx = dso_local local_unnamed_addr constant [4 x i32] [
	i32 39,
	i32 18,
	i32 2,
	i32 28
], align 4

@dso_names_data = dso_local local_unnamed_addr constant [271 x i8] c"libsurface_util_jni.so\00libimage_processing_util_jni.so\00libSystem.Globalization.Native.so\00libSystem.IO.Compression.Native.so\00libSystem.Native.so\00libSystem.Security.Cryptography.Native.Android.so\00libclrjit.so\00libcoreclr.so\00libmscordaccore.so\00libmscordbi.so\00libmonodroid.so\00", align 1

@bundled_assemblies = dso_local local_unnamed_addr global [0 x %struct.XamarinAndroidBundledAssembly] zeroinitializer, align 8

@init_runtime_property_names = dso_local local_unnamed_addr constant [31 x ptr] [
	ptr @.str.0,
	ptr @.str.1,
	ptr @.str.2,
	ptr @.str.3,
	ptr @.str.4,
	ptr @.str.5,
	ptr @.str.6,
	ptr @.str.7,
	ptr @.str.8,
	ptr @.str.9,
	ptr @.str.10,
	ptr @.str.11,
	ptr @.str.12,
	ptr @.str.13,
	ptr @.str.14,
	ptr @.str.15,
	ptr @.str.16,
	ptr @.str.17,
	ptr @.str.18,
	ptr @.str.19,
	ptr @.str.20,
	ptr @.str.21,
	ptr @.str.22,
	ptr @.str.23,
	ptr @.str.24,
	ptr @.str.25,
	ptr @.str.26,
	ptr @.str.27,
	ptr @.str.28,
	ptr @.str.29,
	ptr @.str.30
], align 8

@init_runtime_property_values = dso_local local_unnamed_addr global [31 x ptr] [
	ptr null,
	ptr null,
	ptr null,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.32,
	ptr @.str.32,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.32,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.32,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.32,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.32,
	ptr @.str.31,
	ptr @.str.31,
	ptr @.str.32,
	ptr @.str.32,
	ptr @.str.31,
	ptr @.str.32,
	ptr @.str.31,
	ptr @.str.32
], align 8

@assembly_store_bundled_assemblies = dso_local local_unnamed_addr global [345 x %struct.AssemblyStoreSingleAssemblyRuntimeData] zeroinitializer, align 8

@assembly_store = dso_local local_unnamed_addr global %struct.AssemblyStoreRuntimeData {
	ptr null, 
	i32 0, 
	i32 0, 
	ptr null
}, align 8


@.str.0 = private unnamed_addr constant [22 x i8] c"HOST_RUNTIME_CONTRACT\00", align 1
@.str.1 = private unnamed_addr constant [19 x i8] c"RUNTIME_IDENTIFIER\00", align 1
@.str.2 = private unnamed_addr constant [27 x i8] c"APP_CONTEXT_BASE_DIRECTORY\00", align 1
@.str.3 = private unnamed_addr constant [58 x i8] c"Java.Interop.RuntimeFeature.ManagedPeerNativeRegistration\00", align 1
@.str.4 = private unnamed_addr constant [51 x i8] c"MVVMTOOLKIT_ENABLE_INOTIFYPROPERTYCHANGING_SUPPORT\00", align 1
@.str.5 = private unnamed_addr constant [58 x i8] c"Microsoft.Android.Runtime.RuntimeFeature.IsCoreClrRuntime\00", align 1
@.str.6 = private unnamed_addr constant [55 x i8] c"Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime\00", align 1
@.str.7 = private unnamed_addr constant [60 x i8] c"Microsoft.Android.Runtime.RuntimeFeature.IsNativeAotRuntime\00", align 1
@.str.8 = private unnamed_addr constant [75 x i8] c"Microsoft.Android.Runtime.RuntimeFeature.ManagedToJavaUsesAssemblyFullName\00", align 1
@.str.9 = private unnamed_addr constant [64 x i8] c"Microsoft.Android.Runtime.RuntimeFeature.ObjectReferenceLogging\00", align 1
@.str.10 = private unnamed_addr constant [58 x i8] c"Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap\00", align 1
@.str.11 = private unnamed_addr constant [62 x i8] c"Microsoft.Extensions.DependencyInjection.DisableDynamicEngine\00", align 1
@.str.12 = private unnamed_addr constant [78 x i8] c"Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability\00", align 1
@.str.13 = private unnamed_addr constant [62 x i8] c"Microsoft.Maui.RuntimeFeature.AreBindingInterceptorsSupported\00", align 1
@.str.14 = private unnamed_addr constant [47 x i8] c"Microsoft.Maui.RuntimeFeature.EnableMauiAspire\00", align 1
@.str.15 = private unnamed_addr constant [63 x i8] c"Microsoft.Maui.RuntimeFeature.IsIVisualAssemblyScanningEnabled\00", align 1
@.str.16 = private unnamed_addr constant [60 x i8] c"Microsoft.Maui.RuntimeFeature.IsIncrementalHotReloadEnabled\00", align 1
@.str.17 = private unnamed_addr constant [48 x i8] c"Switch.System.Reflection.ForceInterpretedInvoke\00", align 1
@.str.18 = private unnamed_addr constant [56 x i8] c"System.ComponentModel.DefaultValueAttribute.IsSupported\00", align 1
@.str.19 = private unnamed_addr constant [31 x i8] c"System.Globalization.Invariant\00", align 1
@.str.20 = private unnamed_addr constant [39 x i8] c"System.Linq.Enumerable.IsSizeOptimized\00", align 1
@.str.21 = private unnamed_addr constant [37 x i8] c"System.Net.Http.UseNativeHttpHandler\00", align 1
@.str.22 = private unnamed_addr constant [43 x i8] c"System.Net.SocketsHttpHandler.Http3Support\00", align 1
@.str.23 = private unnamed_addr constant [55 x i8] c"System.Reflection.Metadata.MetadataUpdater.IsSupported\00", align 1
@.str.24 = private unnamed_addr constant [47 x i8] c"System.Runtime.CrashReportBeforeSignalChaining\00", align 1
@.str.25 = private unnamed_addr constant [61 x i8] c"System.Runtime.InteropServices.BuiltInComInterop.IsSupported\00", align 1
@.str.26 = private unnamed_addr constant [70 x i8] c"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization\00", align 1
@.str.27 = private unnamed_addr constant [39 x i8] c"System.StartupHookProvider.IsSupported\00", align 1
@.str.28 = private unnamed_addr constant [46 x i8] c"System.Text.Encoding.EnableUnsafeUTF7Encoding\00", align 1
@.str.29 = private unnamed_addr constant [61 x i8] c"System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault\00", align 1
@.str.30 = private unnamed_addr constant [47 x i8] c"Xamarin.Android.Net.UseNegotiateAuthentication\00", align 1
@.str.31 = private unnamed_addr constant [5 x i8] c"true\00", align 1
@.str.32 = private unnamed_addr constant [6 x i8] c"false\00", align 1

@.ApplicationConfigCLR.0_android_package_name = private unnamed_addr constant [36 x i8] c"com.alexanderparkerinc.eventcheckin\00", align 1

!llvm.module.flags = !{!0, !1, !7, !8, !9, !10}
!0 = !{i32 1, !"wchar_size", i32 4}
!1 = !{i32 7, !"PIC Level", i32 2}
!llvm.ident = !{!2}
!2 = !{!".NET for Android release/11.0.1xx @ c6663f24d64147d204525b3ba0c2762c6176bfc9"}
!3 = !{!4, !4, i64 0}
!4 = !{!"any pointer", !5, i64 0}
!5 = !{!"omnipotent char", !6, i64 0}
!6 = !{!"Simple C++ TBAA"}
!7 = !{i32 1, !"branch-target-enforcement", i32 0}
!8 = !{i32 1, !"sign-return-address", i32 0}
!9 = !{i32 1, !"sign-return-address-all", i32 0}
!10 = !{i32 1, !"sign-return-address-with-bkey", i32 0}
