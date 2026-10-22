namespace Qoot.Api.Content;

/// <summary>Global settings, edited on the Settings page. Contact details entered here appear everywhere on the site.</summary>
public static class SettingsSchema
{
    public static readonly OptionDef[] Languages =
    [
        F.Opt("ar", "Arabic", "العربية"),
        F.Opt("en", "English", "الإنجليزية"),
    ];

    private static readonly OptionDef[] SocialNetworks =
    [
        F.Opt("instagram", "Instagram", "إنستغرام"),
        F.Opt("facebook", "Facebook", "فيسبوك"),
        F.Opt("linkedin", "LinkedIn", "لينكدإن"),
        F.Opt("x", "X", "إكس"),
        F.Opt("youtube", "YouTube", "يوتيوب"),
        F.Opt("whatsapp", "WhatsApp", "واتساب"),
        F.Opt("tiktok", "TikTok", "تيك توك"),
    ];

    private static readonly OptionDef[] TimeZones =
    [
        F.Opt("Africa/Tripoli", "Libya (Africa/Tripoli)", "ليبيا (Africa/Tripoli)"),
        F.Opt("Africa/Cairo", "Egypt (Africa/Cairo)", "مصر (Africa/Cairo)"),
        F.Opt("Africa/Tunis", "Tunisia (Africa/Tunis)", "تونس (Africa/Tunis)"),
        F.Opt("Europe/Malta", "Malta (Europe/Malta)", "مالطا (Europe/Malta)"),
        F.Opt("UTC", "UTC", "UTC"),
    ];

    public static readonly SectionSchema Schema = new()
    {
        Key = "settings",
        Name = new("الإعدادات العامة", "Site settings"),
        Description = new("بيانات التواصل والبحث واللغات وساعات العمل، وتظهر في كل الموقع.", "Contact details, search, languages and hours, used across the whole site."),
        Groups =
        [
            new("identity", new("الهوية", "Identity")),
            new("seo", new("محركات البحث والمشاركة", "Search and sharing")),
            new("contact", new("التواصل", "Contact")),
            new("hours", new("ساعات العمل", "Opening hours")),
            new("languages", new("اللغات", "Languages")),
            new("notifications", new("الإشعارات", "Notifications")),
        ],
        Fields =
        [
            F.Text("siteName", "Business name", "اسم النشاط", 80, required: true, group: "identity"),
            F.Media("logoAr", "Header logo, Arabic pages", "شعار الترويسة، الصفحات العربية", drawings: true, group: "identity"),
            F.Media("logoEn", "Header logo, English pages", "شعار الترويسة، الصفحات الإنجليزية", drawings: true, group: "identity"),
            F.Media("footerMarkAr", "Footer wordmark, Arabic", "شعار التذييل العربي", drawings: true, group: "identity"),
            F.Media("footerMarkEn", "Footer wordmark, English", "شعار التذييل الإنجليزي", drawings: true, group: "identity"),

            F.Plain("siteUrl", "Website address", "عنوان الموقع", 200, TextFormat.AbsoluteUrl, required: true, group: "seo")
                .WithHelp("Used for canonical links, the sitemap and sharing, e.g. https://www.qoot.com", "يُستخدم في الروابط الأساسية وخريطة الموقع والمشاركة، مثل https://www.qoot.com"),
            F.Text("seoTitle", "Page title (search results)", "عنوان الصفحة (نتائج البحث)", 70, required: true, group: "seo"),
            F.Area("seoDescription", "Description (search results)", "الوصف (نتائج البحث)", 170, required: true, group: "seo"),
            F.Media("ogImage", "Share image (1200 × 630)", "صورة المشاركة (1200 × 630)", group: "seo"),
            F.Text("ogImageAlt", "Share image description", "وصف صورة المشاركة", 200, group: "seo"),

            F.Plain("email", "Email", "البريد الإلكتروني", 160, TextFormat.Email, required: true, group: "contact"),
            F.Plain("phone", "Phone (international, digits only)", "الهاتف (دولي، أرقام فقط)", 20, TextFormat.Phone, group: "contact")
                .WithHelp("Used for tap-to-call links, e.g. +218123456789", "يُستخدم في روابط الاتصال، مثل ‎+218123456789"),
            F.Plain("phoneDisplay", "Phone (as shown)", "الهاتف (كما يظهر)", 30, group: "contact"),
            F.Text("address", "Address", "العنوان", 160, group: "contact"),
            F.Link("mapsUrl", "Google Maps link", "رابط خرائط Google", group: "contact"),
            F.List("socialLinks", "Social profiles", "حسابات التواصل الاجتماعي", "network",
            [
                F.Select("network", "Network", "الشبكة", SocialNetworks, required: true),
                F.Link("url", "Profile address", "رابط الحساب", required: true),
            ], max: 7, group: "contact"),

            F.Days("openDays", "Open on", "أيام العمل", group: "hours"),
            F.Time("opensAt", "Opens at", "يفتح الساعة", group: "hours"),
            F.Time("closesAt", "Closes at", "يغلق الساعة", group: "hours"),
            F.Select("timeZone", "Time zone", "المنطقة الزمنية", TimeZones, required: true, group: "hours"),

            F.Select("defaultLanguage", "Main language", "اللغة الأساسية", Languages, required: true, group: "languages")
                .WithHelp("Used when a text has not been translated yet, and as the default for search engines. Arabic pages live at /, English pages at /en/.", "تُستخدم عندما لا يكون النص مترجمًا بعد، وكلغة افتراضية لمحركات البحث. الصفحات العربية على / والإنجليزية على ‎/en/‎."),
            new FieldDef
            {
                Key = "enabledLanguages", Type = FieldType.MultiSelect, Label = new("اللغات المفعّلة", "Languages offered to visitors"),
                Options = Languages, Required = true, Group = "languages",
                Help = new("العربية مفعّلة دائمًا. إيقاف الإنجليزية يخفي زر تبديل اللغة ويحوّل زوار ‎/en/‎ إلى الصفحة العربية.", "Arabic is always on. Turning English off hides the language switch and sends visitors of /en/ to the Arabic page."),
            },

            (F.Plain("notifyEmail", "Send new Contact us messages to", "أرسل رسائل تواصل معنا الجديدة إلى", 160, TextFormat.Email, group: "notifications")
                .WithHelp("Needs the SMTP settings on the server. Leave empty to only collect the messages in the dashboard.", "يتطلب إعدادات SMTP على الخادم. اتركه فارغًا لجمع الرسائل في لوحة التحكم فقط.")) with { Private = true },
        ],
    };
}
