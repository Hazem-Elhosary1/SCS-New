# خرائط العمليات — BPMN 2.0 · Process maps

أربعة مخططات تعاون (collaboration diagrams) بمعيار **BPMN 2.0.2 (OMG) / ISO/IEC 19510:2013**، مرسومة من الكود والوثائق بتاريخ ٢٤ سبتمبر ٢٠٢٦. كل ملف هنا هو BPMN XML كامل مع بيانات الرسم (BPMN DI)، يُفتح في Camunda Modeler أو bpmn.io أو أي أداة متوافقة، ونفس المخططات مرسومة في ملف فيجما الخاص بالعيادة في صفحة **Process Maps — BPMN** (مع README ومفتاح رموز وملاحظات ثنائية اللغة).

| الملف | المخطط | ما يغطيه |
|---|---|---|
| `landscape.bpmn` | Integration landscape | خريطة سياق: أي مسار في المنصة يملك كل تكامل، وما يعبر كل حدود (Meta WhatsApp Cloud API، Telegram Bot API، Fawry، Resend، Anthropic، Twilio) |
| `clinic-visit.bpmn` | Clinic app — patient conversation, booking and visit lifecycle | ترحيب البوت ← تسجيل المريض ← الحجز ← الحضور ← الكشف ← الفاتورة ← الدفع نقدًا |
| `ops-lifecycle.bpmn` | Ops console — clinic onboarding, subscription and support lifecycle | إنشاء العيادة والقنوات ← فوترة التجديد عبر فوري ← التذكيرات والإيقاف التلقائي ← تذاكر الدعم |
| `auth.bpmn` | Sign-in, two-step code, invitation and password reset | الدخول بكلمة المرور ← رمز بالبريد ← JWT؛ روابط الدعوة والاستعادة |

## قواعد القراءة

- كل **حوض** (pool) نظام أو جهة. الحوض المغلق (بلا خطوات) جهة خارجية.
- كل **خط متقطع** يعبر حدود حوض هو استدعاء واجهة أو webhook أو بريد حقيقي موجود في الكود.
- المخططات تصف **ما هو مبني اليوم**؛ المخطّط له ولم يُبنَ يُذكر في الملاحظات (`bpmn:documentation`) لا في الشكل.
- الأسماء بالإنجليزية داخل الأشكال (اصطلاح BPMN) والعربية في `documentation` وفي رسم فيجما تحت كل شكل؛ السطر الرمادي تحت المهمة هو المسار/الحالة/القاعدة الفعلية.

## المصدر والتوليد

المواصفات (pools · lanes · nodes على شبكة أعمدة · flows · messages) في ملف `specs.mjs` داخل مولّد BPMN (سكربت Node في مساحة عمل الجلسة، ومخزّن أيضًا داخل ملف فيجما كـ `scs.kit2/bpmnGen` و`scs.kit2/bpmn`). التوليد ينتج ملفات XML هذه ورسم فيجما من مصدر واحد، فلا يختلفان. لتعديل مخطط: عدّل المواصفة وأعد التوليد؛ لا تحرّك الأشكال يدويًا.

مصدر الحقائق: مسح الكود والوثائق في `scs-platform/src`، و`scs-clinic-web`، و`scs-ops-console`، و`docs/*.md` (خاصة `SYSTEM_INTEGRATION_AUDIT.md` و`INTEGRATIONS_PLAN.md` و`CLINIC_DEV_PLAN.md`). عند تعارض الوثيقة مع الكود اعتُمد الكود.
