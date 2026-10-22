using System.Globalization;

namespace Qoot.Api.Infrastructure;

/// <summary>
/// User-facing API messages in Arabic and English. The language follows the request culture
/// (Accept-Language header or ?lang=), set by the request localization middleware.
/// </summary>
public static class Messages
{
    private static readonly Dictionary<string, (string Ar, string En)> Table = new(StringComparer.Ordinal)
    {
        ["required"] = ("هذا الحقل مطلوب.", "This field is required."),
        ["too_long"] = ("يجب ألا يتجاوز {0} حرفًا.", "Keep it under {0} characters."),
        ["invalid_url"] = ("استخدم ‎#قسم أو ‎/صفحة أو رابطًا يبدأ بـ https:// أو mailto: أو tel:.", "Use #section, /page, a full https:// address, mailto: or tel:."),
        ["invalid_absolute_url"] = ("أدخل عنوانًا كاملًا يبدأ بـ https://.", "Enter a full address starting with https://."),
        ["invalid_email"] = ("أدخل بريدًا إلكترونيًا صحيحًا.", "Enter a valid email address."),
        ["invalid_phone"] = ("استخدم الأرقام فقط، مع + ورمز الدولة.", "Use digits only, starting with + and the country code."),
        ["invalid_option"] = ("اختر أحد الخيارات المتاحة.", "Choose one of the options."),
        ["invalid_number"] = ("أدخل رقمًا صحيحًا بين {0} و{1}.", "Enter a whole number between {0} and {1}."),
        ["invalid_time"] = ("استخدم نظام 24 ساعة، مثل 08:00.", "Use 24-hour time, for example 08:00."),
        ["invalid_days"] = ("اختر أيامًا من الأسبوع.", "Choose days of the week."),
        ["too_many_items"] = ("لا يمكن إضافة أكثر من {0} عنصرًا.", "No more than {0} items."),
        ["too_few_items"] = ("أضف {0} عنصرًا على الأقل.", "Add at least {0} item(s)."),
        ["duplicate_item"] = ("هذا العنصر مكرر.", "This item is listed twice."),
        ["media_not_found"] = ("هذا الملف لم يعد موجودًا في مكتبة الوسائط.", "This file is no longer in the media library."),
        ["media_not_photo"] = ("اختر صورة فوتوغرافية، لا رسمًا.", "Choose a photograph here, not a drawing."),
        ["concurrency_conflict"] = ("حفظ شخص آخر تغييرات في هذه الأثناء. أعد التحميل لرؤية تغييراته.", "Someone else saved changes in the meantime. Reload to see them before saving again."),
        ["not_found"] = ("غير موجود.", "Not found."),
        ["unknown_section"] = ("هذا القسم غير موجود.", "This section doesn't exist."),
        ["section_cannot_hide"] = ("هذا القسم يظهر دائمًا ولا يمكن إخفاؤه.", "This section is always shown and can't be hidden."),
        ["invalid_order"] = ("يجب أن يضم الترتيب كل قسم مرة واحدة.", "The order must list every movable section exactly once."),
        ["nothing_to_publish"] = ("لا توجد تغييرات غير منشورة.", "There are no unpublished changes."),
        ["slug_taken"] = ("هذا الرابط مستخدم في خبر آخر.", "This web address is already used by another article."),
        ["invalid_slug"] = ("استخدم حروفًا إنجليزية صغيرة وأرقامًا وشرطات فقط.", "Use lowercase English letters, numbers and hyphens only."),
        ["title_required"] = ("اكتب عنوانًا بإحدى اللغتين على الأقل.", "Write a title in at least one language."),
        ["category_not_found"] = ("هذا التصنيف غير موجود.", "This category doesn't exist."),
        ["invalid_block"] = ("هذه الفقرة غير صالحة.", "This block is not valid."),
        ["invalid_link_in_text"] = ("رابط غير مسموح في النص. استخدم https:// أو mailto: أو tel: أو /صفحة.", "A link in this text isn't allowed. Use https://, mailto:, tel: or /page."),
        ["publish_needs_title"] = ("لا يمكن نشر خبر بلا عنوان.", "An article needs a title before it can be published."),
        ["media_in_use"] = ("هذا الملف مستخدم في الموقع: {0}. استبدله هناك أولًا.", "This file is used on the site: {0}. Replace it there first."),
        ["media_bundled"] = ("لا يمكن حذف ملفات الهوية المرفقة بالموقع.", "Brand files that ship with the website can't be deleted."),
        ["media_bundled_replace"] = ("لا يمكن استبدال ملفات الهوية المرفقة. ارفع صورة جديدة واخترها في المحتوى.", "Brand files that ship with the website can't be replaced. Upload a new picture and choose it in the content."),
        ["file_missing"] = ("اختر ملفًا لرفعه.", "Choose a file to upload."),
        ["file_too_large"] = ("الحد الأقصى لحجم الملف {0} ميغابايت.", "Files can be up to {0} MB."),
        ["file_type"] = ("ارفع صورة بصيغة JPG أو PNG أو WebP أو AVIF أو GIF.", "Upload a JPG, PNG, WebP, AVIF or GIF image."),
        ["file_corrupt"] = ("هذا الملف ليس صورة صالحة.", "This file isn't a valid image."),
        ["file_dimensions"] = ("أبعاد الصورة كبيرة جدًا (الحد {0} بكسل).", "The image is too large (limit {0} pixels on a side)."),
        ["too_many_files"] = ("يمكن رفع {0} ملفات كحد أقصى في المرة الواحدة.", "Upload at most {0} files at a time."),
        ["login_failed"] = ("البريد الإلكتروني أو كلمة المرور غير صحيحة.", "The email or password is incorrect."),
        ["locked_out"] = ("محاولات كثيرة. حاول مرة أخرى بعد {0} دقيقة.", "Too many attempts. Try again in {0} minutes."),
        ["account_disabled"] = ("هذا الحساب موقوف. تواصل مع أحد المسؤولين.", "This account is turned off. Ask an administrator."),
        ["password_incorrect"] = ("كلمة المرور الحالية غير صحيحة.", "The current password is incorrect."),
        ["password_too_short"] = ("يجب ألا تقل كلمة المرور عن {0} حرفًا.", "Passwords need at least {0} characters."),
        ["password_requires_digit"] = ("يجب أن تحتوي كلمة المرور على رقم.", "Passwords need at least one digit."),
        ["password_requires_lower"] = ("يجب أن تحتوي كلمة المرور على حرف إنجليزي صغير.", "Passwords need a lowercase letter."),
        ["password_requires_upper"] = ("يجب أن تحتوي كلمة المرور على حرف إنجليزي كبير.", "Passwords need an uppercase letter."),
        ["password_requires_unique"] = ("استخدم أحرفًا أكثر تنوعًا في كلمة المرور.", "Use more different characters in the password."),
        ["password_invalid"] = ("كلمة المرور لا تستوفي الشروط.", "The password doesn't meet the requirements."),
        ["email_taken"] = ("يوجد حساب بهذا البريد الإلكتروني.", "An account with this email already exists."),
        ["cannot_change_self"] = ("لا يمكنك إيقاف حسابك أو حذفه أو خفض صلاحياته بنفسك.", "You can't turn off, delete or demote your own account."),
        ["last_admin"] = ("يجب أن يبقى مسؤول واحد نشط على الأقل.", "There must be at least one active administrator."),
        ["invalid_role"] = ("اختر دورًا صحيحًا.", "Choose a valid role."),
        ["invalid_contact"] = ("أدخل رقم هاتف أو بريدًا إلكترونيًا صحيحًا.", "Enter a valid phone number or email address."),
        ["invalid_phone_number"] = ("أدخل رقم هاتف صحيحًا لنتصل بك.", "Enter a phone number we can call you on."),
        ["invalid_status"] = ("اختر حالة صحيحة.", "Choose a valid status."),
        ["arabic_required"] = ("العربية لغة الموقع الأساسية ويجب أن تبقى مفعّلة.", "Arabic is the site's root language and must stay on."),
        ["rate_limited"] = ("طلبات كثيرة. انتظر قليلًا ثم حاول مجددًا.", "Too many requests. Wait a moment and try again."),
        ["server_error"] = ("حدث خطأ من جهتنا. حاول مرة أخرى بعد قليل.", "Something went wrong on our side. Try again in a moment."),
        ["unauthorized"] = ("سجّل الدخول للمتابعة.", "Please sign in to continue."),
        ["forbidden"] = ("ليست لديك صلاحية للقيام بذلك.", "You don't have permission to do this."),
        ["invalid_request"] = ("الطلب غير صالح.", "The request is not valid."),
        ["validation_failed"] = ("بعض الحقول تحتاج إلى تصحيح.", "Some fields need attention."),
        ["antiforgery"] = ("انتهت صلاحية الجلسة. أعد تحميل الصفحة وحاول مجددًا.", "Your session token expired. Reload the page and try again."),
    };

    public static bool IsArabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    public static string Get(string code, params object[] args)
    {
        if (!Table.TryGetValue(code, out var entry))
        {
            return code;
        }

        var template = IsArabic ? entry.Ar : entry.En;
        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }

    public static bool Has(string code) => Table.ContainsKey(code);
}
