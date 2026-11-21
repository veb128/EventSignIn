package crc6468b6408a11370c2f;


public class PasskeysImplementation_CredentialManagerCallback
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		androidx.credentials.CredentialManagerCallback
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onError:(Ljava/lang/Object;)V:GetOnError_Ljava_lang_Object_Handler:AndroidX.Credentials.ICredentialManagerCallbackInvoker, Xamarin.AndroidX.Credentials\n" +
			"n_onResult:(Ljava/lang/Object;)V:GetOnResult_Ljava_lang_Object_Handler:AndroidX.Credentials.ICredentialManagerCallbackInvoker, Xamarin.AndroidX.Credentials\n" +
			"";
		mono.android.Runtime.register ("Microsoft.Maui.Authentication.PasskeysImplementation+CredentialManagerCallback, Microsoft.Maui.Essentials", PasskeysImplementation_CredentialManagerCallback.class, __md_methods);
	}

	public PasskeysImplementation_CredentialManagerCallback ()
	{
		super ();
		if (getClass () == PasskeysImplementation_CredentialManagerCallback.class) {
			mono.android.TypeManager.Activate ("Microsoft.Maui.Authentication.PasskeysImplementation+CredentialManagerCallback, Microsoft.Maui.Essentials", "", this, new java.lang.Object[] {  });
		}
	}

	public void onError (java.lang.Object p0)
	{
		n_onError (p0);
	}

	private native void n_onError (java.lang.Object p0);

	public void onResult (java.lang.Object p0)
	{
		n_onResult (p0);
	}

	private native void n_onResult (java.lang.Object p0);

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
