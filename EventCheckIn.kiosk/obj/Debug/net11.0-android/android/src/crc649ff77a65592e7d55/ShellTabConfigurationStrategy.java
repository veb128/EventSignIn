package crc649ff77a65592e7d55;


public class ShellTabConfigurationStrategy
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		com.google.android.material.tabs.TabLayoutMediator.TabConfigurationStrategy
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onConfigureTab:(Lcom/google/android/material/tabs/TabLayout$Tab;I)V:GetOnConfigureTab_Lcom_google_android_material_tabs_TabLayout_Tab_IHandler:Google.Android.Material.Tabs.TabLayoutMediator+ITabConfigurationStrategyInvoker, Xamarin.Google.Android.Material\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Handlers.ShellTabConfigurationStrategy, Microsoft.Maui.Controls", ShellTabConfigurationStrategy.class, __md_methods);
	}

	public ShellTabConfigurationStrategy ()
	{
		super ();
		if (getClass () == ShellTabConfigurationStrategy.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.ShellTabConfigurationStrategy, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
		}
	}

	public void onConfigureTab (com.google.android.material.tabs.TabLayout.Tab p0, int p1)
	{
		n_onConfigureTab (p0, p1);
	}

	private native void n_onConfigureTab (com.google.android.material.tabs.TabLayout.Tab p0, int p1);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
