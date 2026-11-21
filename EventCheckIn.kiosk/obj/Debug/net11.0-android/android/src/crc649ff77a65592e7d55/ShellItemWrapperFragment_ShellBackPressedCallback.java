package crc649ff77a65592e7d55;


public class ShellItemWrapperFragment_ShellBackPressedCallback
	extends androidx.activity.OnBackPressedCallback
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_handleOnBackPressed:()V:GetHandleOnBackPressedHandler\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Controls.Handlers.ShellItemWrapperFragment+ShellBackPressedCallback, Microsoft.Maui.Controls", ShellItemWrapperFragment_ShellBackPressedCallback.class, __md_methods);
	}

	public ShellItemWrapperFragment_ShellBackPressedCallback (boolean p0)
	{
		super (p0);
		if (getClass () == ShellItemWrapperFragment_ShellBackPressedCallback.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Controls.Handlers.ShellItemWrapperFragment+ShellBackPressedCallback, Microsoft.Maui.Controls", "System.Boolean, System.Private.CoreLib", this, new java.lang.Object[] { p0 });
		}
	}

	public void handleOnBackPressed ()
	{
		n_handleOnBackPressed ();
	}

	private native void n_handleOnBackPressed ();

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
