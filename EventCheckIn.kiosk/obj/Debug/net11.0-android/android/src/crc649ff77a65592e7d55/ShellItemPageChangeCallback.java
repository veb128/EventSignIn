package crc649ff77a65592e7d55;


public class ShellItemPageChangeCallback
	extends androidx.viewpager2.widget.ViewPager2.OnPageChangeCallback
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onPageSelected:(I)V:GetOnPageSelected_IHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Handlers.ShellItemPageChangeCallback, Microsoft.Maui.Controls", ShellItemPageChangeCallback.class, __md_methods);
	}

	public ShellItemPageChangeCallback ()
	{
		super ();
		if (getClass () == ShellItemPageChangeCallback.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.ShellItemPageChangeCallback, Microsoft.Maui.Controls", "", this, new java.lang.Object[] {  });
		}
	}

	public void onPageSelected (int p0)
	{
		n_onPageSelected (p0);
	}

	private native void n_onPageSelected (int p0);

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
