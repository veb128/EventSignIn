source_filename = "jni_remap.x86_64.ll"
target datalayout = "e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128"
target triple = "x86_64-unknown-linux-android21"

%struct.JniRemappingIndexMethodEntry = type {
	%struct.JniRemappingString, 
	%struct.JniRemappingString, 
	%struct.JniRemappingReplacementMethod 
}

%struct.JniRemappingIndexTypeEntry = type {
	%struct.JniRemappingString, 
	i32, 
	ptr 
}

%struct.JniRemappingReplacementMethod = type {
	ptr, 
	ptr, 
	i1 
}

%struct.JniRemappingString = type {
	i32, 
	ptr 
}

%struct.JniRemappingTypeReplacementEntry = type {
	%struct.JniRemappingString, 
	ptr 
}

@jni_remapping_type_replacements = dso_local local_unnamed_addr constant %struct.JniRemappingTypeReplacementEntry zeroinitializer, align 8

@jni_remapping_method_replacement_index = dso_local local_unnamed_addr constant %struct.JniRemappingIndexTypeEntry zeroinitializer, align 8

!llvm.module.flags = !{!0, !1}
!0 = !{i32 1, !"wchar_size", i32 4}
!1 = !{i32 7, !"PIC Level", i32 2}
!llvm.ident = !{!2}
!2 = !{!".NET for Android release/11.0.1xx @ c6663f24d64147d204525b3ba0c2762c6176bfc9"}
!3 = !{!4, !4, i64 0}
!4 = !{!"any pointer", !5, i64 0}
!5 = !{!"omnipotent char", !6, i64 0}
!6 = !{!"Simple C++ TBAA"}
