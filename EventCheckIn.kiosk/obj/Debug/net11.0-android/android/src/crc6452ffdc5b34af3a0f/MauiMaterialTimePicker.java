package crc6452ffdc5b34af3a0f;


public class MauiMaterialTimePicker
	extends crc6452ffdc5b34af3a0f.MauiMaterialDateTimePickerBase
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Platform.MauiMaterialTimePicker, Microsoft.Maui", MauiMaterialTimePicker.class, __md_methods);
	}

	public MauiMaterialTimePicker (android.content.Context p0)
	{
		super (p0);
		if (getClass () == MauiMaterialTimePicker.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.MauiMaterialTimePicker, Microsoft.Maui", "Android.Content.Context, Mono.Android", this, new java.lang.Object[] { p0 });
		}
	}

	public MauiMaterialTimePicker (android.content.Context p0, android.util.AttributeSet p1)
	{
		super (p0, p1);
		if (getClass () == MauiMaterialTimePicker.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.MauiMaterialTimePicker, Microsoft.Maui", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android", this, new java.lang.Object[] { p0, p1 });
		}
	}

	public MauiMaterialTimePicker (android.content.Context p0, android.util.AttributeSet p1, int p2)
	{
		super (p0, p1, p2);
		if (getClass () == MauiMaterialTimePicker.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Platform.MauiMaterialTimePicker, Microsoft.Maui", "Android.Content.Context, Mono.Android:Android.Util.IAttributeSet, Mono.Android:System.Int32, System.Private.CoreLib", this, new java.lang.Object[] { p0, p1, p2 });
		}
	}

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
