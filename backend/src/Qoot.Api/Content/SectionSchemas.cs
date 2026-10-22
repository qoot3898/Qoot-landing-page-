namespace Qoot.Api.Content;

/// <summary>
/// The editable structure of every part of the Qoot page. Keys match the public renderer in
/// frontend/src/public/sections. Adding a field here makes it editable in the dashboard and
/// validated by the API; the renderer decides where it appears.
/// </summary>
public static class SectionSchemas
{
    public const string Header = "header";
    public const string Hero = "hero";
    public const string Supplies = "supplies";
    public const string How = "how";
    public const string Who = "who";
    public const string News = "news";
    public const string Faq = "faq";
    public const string Contact = "contact";
    public const string Visit = "visit";
    public const string Footer = "footer";

    private static readonly OptionDef[] TileTones =
    [
        F.Opt("veg", "Leaf green (produce)", "أخضر ورقي (الخضار)"),
        F.Opt("poultry", "Coral (meat)", "مرجاني (اللحوم)"),
        F.Opt("fish", "Sea blue (fish)", "أزرق بحري (الأسماك)"),
        F.Opt("dry", "Ochre (grain)", "مغرة (الحبوب)"),
        F.Opt("dairy", "Milk white (dairy)", "أبيض حليبي (الألبان)"),
        F.Opt("bakery", "Wheat (bread)", "قمحي (المخبوزات)"),
        F.Opt("water", "Aqua (water)", "مائي (المياه)"),
        F.Opt("pack", "Kraft (packaging)", "ورق الكرافت (التغليف)"),
    ];

    private static readonly OptionDef[] TileSizes =
    [
        F.Opt("auto", "Automatic (by colour)", "تلقائي (حسب اللون)"),
        F.Opt("3", "Small (3/12)", "صغير (3/12)"),
        F.Opt("4", "Medium (4/12)", "متوسط (4/12)"),
        F.Opt("5", "Large (5/12)", "كبير (5/12)"),
        F.Opt("7", "Wide (7/12)", "عريض (7/12)"),
        F.Opt("12", "Full width", "بعرض الصف"),
    ];

    private static readonly OptionDef[] StepTones =
    [
        F.Opt("leaf", "Leaf green", "أخضر ورقي"),
        F.Opt("green", "Deep green", "أخضر داكن"),
        F.Opt("ochre", "Ochre", "مغرة"),
        F.Opt("sea", "Sea blue", "أزرق بحري"),
    ];

    public static readonly OptionDef[] SectorIcons =
    [
        F.Opt("oil", "Oil rig", "منصة نفط"),
        F.Opt("canteen", "Plate and cutlery", "طبق وأدوات مائدة"),
        F.Opt("hospital", "Medical cross", "صليب طبي"),
        F.Opt("school", "Open book", "كتاب مفتوح"),
        F.Opt("restaurant", "Serving dish", "طبق تقديم"),
        F.Opt("events", "Celebration cake", "كعكة احتفال"),
    ];

    /// <summary>The cities drawn on the Libya map. Their position is fixed in the map drawing.</summary>
    public static readonly OptionDef[] MapCities =
    [
        F.Opt("tripoli", "Tripoli", "طرابلس"),
        F.Opt("misrata", "Misrata", "مصراتة"),
        F.Opt("sirte", "Sirte", "سرت"),
        F.Opt("raslanuf", "Ras Lanuf", "رأس لانوف"),
        F.Opt("brega", "Brega", "البريقة"),
        F.Opt("ajdabiya", "Ajdabiya", "أجدابيا"),
        F.Opt("bayda", "Al Bayda", "البيضاء"),
        F.Opt("derna", "Derna", "درنة"),
        F.Opt("tobruk", "Tobruk", "طبرق"),
        F.Opt("jalu", "Jalu", "جالو"),
        F.Opt("kufra", "Kufra", "الكفرة"),
        F.Opt("sabha", "Sabha", "سبها"),
        F.Opt("ubari", "Ubari", "أوباري"),
        F.Opt("ghat", "Ghat", "غات"),
    ];

    private static readonly GroupDef[] ContactGroups =
    [
        new("text", new("النصوص", "Text")),
        new("form", new("حقول النموذج", "Form fields")),
        new("messages", new("رسائل النموذج", "Form messages")),
    ];

    public static readonly IReadOnlyList<SectionSchema> All =
    [
        new()
        {
            Key = Header,
            Name = new("الترويسة والقائمة", "Header and navigation"),
            Description = new("الشعار وروابط القائمة وزر تواصل معنا في أعلى كل صفحة.", "The logo, menu links and the Contact us button at the top of every page."),
            Pinned = true,
            CanHide = false,
            Fields =
            [
                F.List("links", "Menu links", "روابط القائمة", "label",
                [
                    F.Text("label", "Label", "النص", 40, required: true),
                    F.Link("target", "Goes to", "يذهب إلى", required: true),
                ], max: 7),
                F.Text("ctaLabel", "Button label", "نص الزر", 40, required: true),
                F.Link("ctaTarget", "Button goes to", "الزر يذهب إلى", required: true),
                F.Text("menuOpenLabel", "Menu button (phones)", "زر القائمة (الهواتف)", 20),
                F.Text("menuCloseLabel", "Close menu label", "نص إغلاق القائمة", 20),
                F.Text("skipLabel", "Skip link (keyboard and screen readers)", "رابط التخطي (لوحة المفاتيح وقارئات الشاشة)", 60),
                F.Text("logoAlt", "Logo description", "وصف الشعار", 160),
            ],
        },
        new()
        {
            Key = Hero,
            Name = new("الواجهة الرئيسية", "Hero"),
            Description = new("الاسم مقسومًا حول نافذة Q، والصور التي تمر عبرها، والجملة التعريفية.", "The name split around the Q window, the pictures that pass through it, and the opening sentence."),
            CanHide = false,
            Fields =
            [
                F.Text("titleStart", "Title, first half", "العنوان، الجزء الأول", 40, required: true)
                    .WithHelp("Shown on the reading-start side of the Q window.", "يظهر في جهة بداية القراءة من نافذة Q."),
                F.Text("titleEnd", "Title, second half", "العنوان، الجزء الثاني", 60),
                F.Area("lede", "Opening sentence", "الجملة التعريفية", 300, required: true),
                F.Text("linkLabel", "Link label", "نص الرابط", 40),
                F.Link("linkTarget", "Link goes to", "الرابط يذهب إلى"),
                F.List("slides", "Pictures in the Q window", "الصور في نافذة Q", "caption",
                [
                    F.Media("image", "Picture", "الصورة", required: true),
                    F.Text("caption", "Caption", "التعليق", 80),
                ], max: 8, min: 1),
            ],
        },
        new()
        {
            Key = Supplies,
            Name = new("ما نوفّره", "What we supply"),
            Description = new("رف الأصناف: بطاقة ملونة لكل فئة مع رسمها ومنتجاتها وموعد توصيلها.", "The shelf: one coloured card per category with its drawing, products and delivery rhythm."),
            Anchor = "supplies",
            Fields =
            [
                F.Text("title", "Heading", "العنوان", 60, required: true),
                F.Area("intro", "Introduction", "المقدمة", 300),
                F.List("tiles", "Categories", "الفئات", "name",
                [
                    F.Text("name", "Name", "الاسم", 60, required: true),
                    F.Select("tone", "Colour", "اللون", TileTones, required: true),
                    F.Select("size", "Card size", "حجم البطاقة", TileSizes),
                    F.Text("when", "Delivery rhythm", "موعد التوصيل", 60),
                    F.List("items", "Products", "المنتجات", "text",
                    [
                        F.Text("text", "Products", "المنتجات", 160, required: true),
                        F.Text("detail", "Packing or detail", "التعبئة أو التفاصيل", 120),
                    ], max: 8),
                    F.Text("short", "Short summary (phones)", "ملخص قصير (الهواتف)", 80),
                    F.Media("art", "Drawing", "الرسم", drawings: true),
                ], max: 16),
                F.Text("notePrefix", "Note before the link", "الملاحظة قبل الرابط", 160),
                F.Text("noteLinkLabel", "Note link", "رابط الملاحظة", 80),
                F.Link("noteLinkTarget", "Note link goes to", "رابط الملاحظة يذهب إلى"),
                F.Text("noteSuffix", "Note after the link", "الملاحظة بعد الرابط", 160),
            ],
        },
        new()
        {
            Key = How,
            Name = new("طريقة عملنا", "How we work"),
            Description = new("بطاقات الخطوات الملونة التي تتراكم أثناء التمرير.", "The coloured step cards that stack as you scroll."),
            Anchor = "how-we-work",
            Fields =
            [
                F.Text("title", "Heading", "العنوان", 60, required: true),
                F.Area("intro", "Introduction", "المقدمة", 300),
                F.List("steps", "Steps", "الخطوات", "title",
                [
                    F.Text("title", "Step name", "اسم الخطوة", 30, required: true),
                    F.Area("text", "Sentence", "الجملة", 200),
                    F.Select("tone", "Colour", "اللون", StepTones, required: true),
                    F.Media("image", "Picture", "الصورة"),
                    F.Number("focusX", "Picture focus, across (%)", "تركيز الصورة أفقيًا (%)", 0, 100),
                    F.Number("focusY", "Picture focus, down (%)", "تركيز الصورة عموديًا (%)", 0, 100),
                ], max: 6),
            ],
        },
        new()
        {
            Key = Who,
            Name = new("من نخدم", "Who we feed"),
            Description = new("القطاعات التي نخدمها، وخريطة ليبيا، والمعلومات السريعة.", "The sectors we serve, the map of Libya and the quick facts."),
            Anchor = "who-we-feed",
            Groups =
            [
                new("text", new("النصوص والقطاعات", "Text and sectors")),
                new("map", new("الخريطة", "Map")),
                new("facts", new("المعلومات وزر الطلب", "Facts and button")),
            ],
            Fields =
            [
                F.Text("title", "Heading", "العنوان", 60, required: true, group: "text"),
                F.Area("lede", "Introduction", "المقدمة", 240, group: "text"),
                F.List("sectors", "Sectors", "القطاعات", "label",
                [
                    F.Text("label", "Name", "الاسم", 60, required: true),
                    F.Icon("icon", "Icon", "الأيقونة", SectorIcons),
                ], max: 9, group: "text"),
                F.Text("meaningTitle", "Name meaning, heading", "معنى الاسم، العنوان", 60, group: "text"),
                F.Area("meaningText", "Name meaning, text", "معنى الاسم، النص", 300, group: "text"),
                F.Text("mapIntro", "Map description (screen readers)", "وصف الخريطة (قارئات الشاشة)", 160, group: "map")
                    .WithHelp("The visible city names are added after this sentence.", "تُضاف أسماء المدن الظاهرة بعد هذه الجملة."),
                F.Text("hqLabel", "Base city label", "اسم مدينة المقر", 40, group: "map"),
                F.List("cities", "Cities on the map", "المدن على الخريطة", "label",
                [
                    F.Select("city", "City", "المدينة", MapCities, required: true),
                    F.Text("label", "Name shown", "الاسم الظاهر", 40, required: true),
                    F.Bool("showLabel", "Show the name on the map", "إظهار الاسم على الخريطة"),
                ], max: 14, group: "map"),
                F.List("facts", "Quick facts", "معلومات سريعة", "label",
                [
                    F.Text("label", "Label", "العنوان", 40, required: true),
                    F.Text("value", "Value", "القيمة", 120, required: true),
                ], max: 4, group: "facts"),
                F.Text("ctaLabel", "Button label", "نص الزر", 40, group: "facts"),
                F.Link("ctaTarget", "Button goes to", "الزر يذهب إلى", group: "facts"),
            ],
        },
        new()
        {
            Key = News,
            Name = new("آخر الأخبار", "Latest news"),
            Description = new("أحدث الأخبار المنشورة، تُدار من صفحة الأخبار.", "The newest published articles; the articles themselves are managed under News."),
            Anchor = "news",
            Fields =
            [
                F.Text("title", "Heading", "العنوان", 60, required: true),
                F.Area("intro", "Introduction", "المقدمة", 240),
                F.Number("homeCount", "Articles shown on the home page", "عدد الأخبار في الصفحة الرئيسية", 1, 6),
                F.Text("viewAllLabel", "Link to all news", "رابط كل الأخبار", 40),
                F.Text("readMoreLabel", "Read more label", "نص اقرأ المزيد", 40),
                F.Text("emptyText", "Message when there is no news", "الرسالة عند عدم وجود أخبار", 160),
                F.Bool("hideWhenEmpty", "Hide this section on the home page while there is no news", "إخفاء هذا القسم في الرئيسية ما دامت لا توجد أخبار"),
            ],
        },
        new()
        {
            Key = Faq,
            Name = new("الأسئلة الشائعة", "Questions"),
            Description = new("الأسئلة والأجوبة، ووسائل التواصل بجانبها.", "Questions and answers, with the contact details beside them."),
            Anchor = "faq",
            Fields =
            [
                F.Text("title", "Heading", "العنوان", 80, required: true),
                F.Text("sideTitle", "Contact column heading", "عنوان عمود التواصل", 40),
                F.List("items", "Questions", "الأسئلة", "question",
                [
                    F.Text("question", "Question", "السؤال", 160, required: true),
                    F.Area("answer", "Answer", "الجواب", 800, required: true),
                ], max: 20),
                F.Text("morePrefix", "Line under the questions", "السطر تحت الأسئلة", 120),
                F.Text("moreLinkLabel", "Email link", "رابط البريد", 120)
                    .WithHelp("{email} inserts the email address from Settings.", "‏{email} يُدرج البريد الإلكتروني من الإعدادات."),
            ],
        },
        new()
        {
            Key = Contact,
            Name = new("تواصل معنا", "Contact us"),
            Description = new("نموذج تواصل معنا ووسائل التواصل المباشر. الرسائل تصل إلى صفحة تواصل معنا في لوحة التحكم.", "The Contact us form and the direct contact details. Messages arrive under Contact us in the dashboard."),
            Anchor = "contact",
            Groups = ContactGroups,
            Fields =
            [
                F.Text("title", "Heading", "العنوان", 60, required: true, group: "text"),
                F.Area("sub", "Sentence under the heading", "الجملة تحت العنوان", 200, group: "text"),
                F.Text("directLabel", "Direct contact heading", "عنوان التواصل المباشر", 60, group: "text"),
                F.Text("whatsappLabel", "WhatsApp link", "رابط واتساب", 40, group: "text")
                    .WithHelp("Opens a WhatsApp chat with the phone number from Settings. Leave empty to hide it.", "يفتح محادثة واتساب مع رقم الهاتف من الإعدادات. اتركه فارغًا لإخفائه."),
                F.Media("image", "Picture in the Q window", "الصورة داخل نافذة Q", group: "text"),
                F.Text("nameLabel", "Name (label)", "الاسم (العنوان)", 60, required: true, group: "form"),
                F.Text("namePlaceholder", "Name (example)", "الاسم (مثال)", 80, group: "form"),
                F.Text("phoneLabel", "Phone (label)", "الهاتف (العنوان)", 60, required: true, group: "form"),
                F.Text("phonePlaceholder", "Phone (example)", "الهاتف (مثال)", 80, group: "form"),
                F.Text("emailLabel", "Email (label)", "البريد الإلكتروني (العنوان)", 60, required: true, group: "form"),
                F.Text("emailPlaceholder", "Email (example)", "البريد الإلكتروني (مثال)", 80, group: "form"),
                F.Text("optionalLabel", "\"Optional\" mark", "علامة \"اختياري\"", 20, group: "form"),
                F.Text("messageLabel", "Message (label)", "الرسالة (العنوان)", 60, required: true, group: "form"),
                F.Text("messagePlaceholder", "Message (example)", "الرسالة (مثال)", 120, group: "form"),
                F.Text("submitLabel", "Send button", "زر الإرسال", 40, required: true, group: "form"),
                F.Text("msgNameMissing", "When the name is missing", "عند عدم كتابة الاسم", 120, group: "messages"),
                F.Text("msgPhoneInvalid", "When the phone number is missing or not valid", "عند غياب رقم الهاتف أو خطئه", 120, group: "messages"),
                F.Text("msgEmailInvalid", "When the email is not valid", "عند خطأ البريد الإلكتروني", 120, group: "messages"),
                F.Text("msgMessageMissing", "When the message is empty", "عند عدم كتابة الرسالة", 120, group: "messages"),
                F.Text("msgSending", "While sending", "أثناء الإرسال", 120, group: "messages"),
                F.Area("msgSuccess", "After sending", "بعد الإرسال", 240, group: "messages"),
                F.Area("msgError", "When sending fails", "عند فشل الإرسال", 240, group: "messages"),
            ],
        },
        new()
        {
            Key = Visit,
            Name = new("زورونا", "Visit us"),
            Description = new("خريطة الساحل، ساعات العمل وحالة الفتح الآن.", "The coast map, opening hours and whether you are open right now."),
            Anchor = "location",
            Groups =
            [
                new("text", new("النصوص", "Text")),
                new("hours", new("ساعات العمل", "Opening hours")),
                new("map", new("الخريطة", "Map")),
            ],
            Fields =
            [
                F.Text("title", "Heading", "العنوان", 60, required: true, group: "text"),
                F.Area("lede", "Introduction", "المقدمة", 240, group: "text"),
                F.Text("mapsLabel", "Maps button", "زر الخرائط", 40, group: "text"),
                F.Text("callLabel", "Call link", "رابط الاتصال", 40, group: "text"),
                F.Text("newTabLabel", "\"Opens in a new tab\" (screen readers)", "\"يُفتح في نافذة جديدة\" (قارئات الشاشة)", 60, group: "text"),
                F.List("hoursRows", "Hours table", "جدول الساعات", "label",
                [
                    F.Text("label", "Days", "الأيام", 60, required: true),
                    F.Text("value", "Hours", "الساعات", 60, required: true),
                    F.Days("days", "Highlight on", "يُبرز في", null),
                ], max: 7, group: "hours"),
                F.Text("statusOpen", "Open now", "مفتوح الآن", 80, group: "hours").WithHelp("{to} = closing time", "‏{to} = وقت الإغلاق"),
                F.Text("statusToday", "Closed, opens later today", "مغلق، نفتح لاحقًا اليوم", 80, group: "hours").WithHelp("{from} = opening time", "‏{from} = وقت الفتح"),
                F.Text("statusTomorrow", "Closed, opens tomorrow", "مغلق، نفتح غدًا", 80, group: "hours").WithHelp("{from} = opening time", "‏{from} = وقت الفتح"),
                F.Text("statusDay", "Closed, opens another day", "مغلق، نفتح يومًا آخر", 80, group: "hours").WithHelp("{day} = day name, {from} = opening time", "‏{day} = اسم اليوم، {from} = وقت الفتح"),
                F.Text("clock", "Local time line", "سطر الوقت المحلي", 80, group: "hours").WithHelp("{time} = time in Benghazi", "‏{time} = الوقت في بنغازي"),
                F.Text("mapTitle", "Map description (screen readers)", "وصف الخريطة (قارئات الشاشة)", 160, group: "map"),
                F.Text("seaLabel", "Sea label", "اسم البحر", 40, group: "map"),
                F.Text("cityLabel", "City label", "اسم المدينة", 40, group: "map"),
                F.Text("scaleLabel", "Scale label", "مقياس الرسم", 20, group: "map"),
            ],
        },
        new()
        {
            Key = Footer,
            Name = new("التذييل", "Footer"),
            Description = new("شريط الرسومات، الشعاران، وسائل التواصل والأسطر القانونية.", "The drawing band, both wordmarks, contact details and the legal lines."),
            Pinned = true,
            CanHide = false,
            Fields =
            [
                F.List("lines", "Small print lines", "الأسطر الصغيرة", "text",
                [
                    F.Text("text", "Line", "السطر", 160, required: true).WithHelp("{year} inserts the current year.", "‏{year} يُدرج السنة الحالية."),
                ], max: 6),
                F.Text("backToTopLabel", "Back to top", "العودة إلى الأعلى", 40),
                F.Text("contactLabel", "Footer description (screen readers)", "وصف التذييل (قارئات الشاشة)", 60),
            ],
        },
    ];

    public static readonly IReadOnlyDictionary<string, SectionSchema> ByKey = All.ToDictionary(s => s.Key, StringComparer.Ordinal);

    public static SectionSchema? Find(string key) => ByKey.GetValueOrDefault(key);

    /// <summary>The order sections take when the site is first created (header first, footer last).</summary>
    public static readonly string[] DefaultOrder = [Header, Hero, Supplies, How, Who, News, Faq, Contact, Visit, Footer];
}
