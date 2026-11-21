#include <stdlib.h>

static void xamarin_initialize_dotnet ()
{
	setenv ("DOTNET_MODIFIABLE_ASSEMBLIES", "debug", 1);
}

extern "C" void xamarin_initialize_dotnet();
extern "C" void xamarin_create_classes_Microsoft_iOS();

static void xamarin_invoke_registration_methods ()
{
	xamarin_initialize_dotnet();
	xamarin_create_classes_Microsoft_iOS();
}

#include "xamarin/xamarin.h"


void xamarin_register_modules_impl ()
{

}

void xamarin_register_assemblies_impl ()
{

}

static const char *xamarin_runtime_libraries_array[] = {
	NULL
};

static const char *xamarin_runtime_config_property_keys_array [] = {
	"MVVMTOOLKIT_ENABLE_INOTIFYPROPERTYCHANGING_SUPPORT",
	"Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability",
	"Microsoft.Maui.RuntimeFeature.AreBindingInterceptorsSupported",
	"Microsoft.Maui.RuntimeFeature.EnableMauiAspire",
	"Microsoft.Maui.RuntimeFeature.IsIVisualAssemblyScanningEnabled",
	"Microsoft.Maui.RuntimeFeature.IsIncrementalHotReloadEnabled",
	"ObjCRuntime.Runtime.HotReloadCompatible",
	"System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization",
	"System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported",
	"System.Diagnostics.Debugger.IsSupported",
	"System.Globalization.Hybrid",
	"System.Globalization.Invariant",
	"System.Linq.Enumerable.IsSizeOptimized",
	"System.Net.Http.UseNativeHttpHandler",
	"System.Net.SocketsHttpHandler.Http3Support",
	"System.Reflection.Metadata.MetadataUpdater.IsSupported",
	"System.Reflection.NullabilityInfoContext.IsSupported",
	"System.Resources.ResourceManager.AllowCustomResourceTypes",
	"System.Resources.UseSystemResourceKeys",
	"System.Runtime.InteropServices.BuiltInComInterop.IsSupported",
	"System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting",
	"System.Runtime.InteropServices.EnableCppCLIHostActivation",
	"System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization",
	"System.StartupHookProvider.IsSupported",
	"System.Text.Encoding.EnableUnsafeUTF7Encoding",
	"System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault",
	"System.Threading.Thread.EnableAutoreleasePool",
};
static const char *xamarin_runtime_config_property_values_array [] = {
	"true",
	"true",
	"true",
	"true",
	"false",
	"true",
	"true",
	"false",
	"false",
	"true",
	"true",
	"false",
	"true",
	"true",
	"false",
	"true",
	"false",
	"false",
	"false",
	"false",
	"false",
	"false",
	"false",
	"true",
	"false",
	"true",
	"true",
};
void xamarin_setup_impl ()
{
	xamarin_invoke_registration_methods ();
	xamarin_libmono_native_link_mode = XamarinNativeLinkModeDynamicLibrary;
	xamarin_runtime_libraries = xamarin_runtime_libraries_array;
	xamarin_init_mono_debug = TRUE;
	xamarin_executable_name = "EventCheckIn.dll";
	xamarin_log_level = 0;
	xamarin_arch_name = "arm64";
	xamarin_marshal_objectivec_exception_mode = MarshalObjectiveCExceptionModeThrowManagedException;
	xamarin_debug_mode = TRUE;
	xamarin_initialize_dynamic_registrar ();
	xamarin_supports_dynamic_registration = TRUE;
	xamarin_runtime_configuration_name = NULL;
	xamarin_runtime_config_property_count = 27;
	xamarin_runtime_config_property_keys = xamarin_runtime_config_property_keys_array;
	xamarin_runtime_config_property_values = xamarin_runtime_config_property_values_array;
}

int main (int argc, char **argv)
{
	NSAutoreleasePool *pool = [[NSAutoreleasePool alloc] init];
	int rv = xamarin_main (argc, argv, XamarinLaunchModeApp);
	[pool drain];
	return rv;
}

void xamarin_initialize_callbacks () __attribute__ ((constructor));
void xamarin_initialize_callbacks ()
{
	xamarin_setup = xamarin_setup_impl;
	xamarin_register_assemblies = xamarin_register_assemblies_impl;
	xamarin_register_modules = xamarin_register_modules_impl;
}
