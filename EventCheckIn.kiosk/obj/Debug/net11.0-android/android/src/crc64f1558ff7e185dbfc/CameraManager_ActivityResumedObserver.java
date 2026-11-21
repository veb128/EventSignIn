package crc64f1558ff7e185dbfc;


public class CameraManager_ActivityResumedObserver
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.lifecycle.LifecycleEventObserver
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onStateChanged:(Landroidx/lifecycle/LifecycleOwner;Landroidx/lifecycle/Lifecycle$Event;)V:GetOnStateChanged_Landroidx_lifecycle_LifecycleOwner_Landroidx_lifecycle_Lifecycle_Event_Handler:AndroidX.Lifecycle.ILifecycleEventObserverInvoker, Xamarin.AndroidX.Lifecycle.Common.Jvm\n" +
			"";
		mono.android.Runtime.register ("CommunityToolkit.Maui.Core.CameraManager+ActivityResumedObserver, CommunityToolkit.Maui.Camera", CameraManager_ActivityResumedObserver.class, __md_methods);
	}

	public CameraManager_ActivityResumedObserver ()
	{
		super ();
		if (getClass () == CameraManager_ActivityResumedObserver.class) {
			mono.android.TypeManager.Activate ("CommunityToolkit.Maui.Core.CameraManager+ActivityResumedObserver, CommunityToolkit.Maui.Camera", "", this, new java.lang.Object[] {  });
		}
	}

	public void onStateChanged (androidx.lifecycle.LifecycleOwner p0, androidx.lifecycle.Lifecycle.Event p1)
	{
		n_onStateChanged (p0, p1);
	}

	private native void n_onStateChanged (androidx.lifecycle.LifecycleOwner p0, androidx.lifecycle.Lifecycle.Event p1);

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
