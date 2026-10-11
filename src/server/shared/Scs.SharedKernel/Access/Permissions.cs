// Generated from specs/005-permissions-engine/contracts/permission-catalogue.md; change the
// contract and this file together. PermissionCatalogueTests holds the two equal.
namespace Scs.SharedKernel.Access;

/// <summary>
/// The code of every permission of the clinic console, one constant each (BRD Volume 1,
/// Chapter 10 §2-3). A route or a control names one of these; a misspelt code does not compile.
/// </summary>
public static class Permissions
{
    /// <summary>1. View bookings.</summary>
    public const string BookingsView = "bookings.view";

    /// <summary>2. View all doctors' appointments.</summary>
    public const string BookingsViewAll = "bookings.viewAll";

    /// <summary>3. Manage bookings.</summary>
    public const string BookingsManage = "bookings.manage";

    /// <summary>4. Cancel appointments.</summary>
    public const string BookingsCancel = "bookings.cancel";

    /// <summary>5. Check patients in and record no-shows.</summary>
    public const string BookingsAttendance = "bookings.attendance";

    /// <summary>6. Manage today's queue.</summary>
    public const string BookingsQueue = "bookings.queue";

    /// <summary>7. Manage the waiting list.</summary>
    public const string BookingsWaitlist = "bookings.waitlist";

    /// <summary>8. View patients.</summary>
    public const string PatientsView = "patients.view";

    /// <summary>9. Manage patient files.</summary>
    public const string PatientsManage = "patients.manage";

    /// <summary>10. Delete patient files.</summary>
    public const string PatientsDelete = "patients.delete";

    /// <summary>11. Merge patient files.</summary>
    public const string PatientsMerge = "patients.merge";

    /// <summary>12. View medical records.</summary>
    public const string PatientsRecordsView = "patients.records.view";

    /// <summary>13. Edit exams.</summary>
    public const string PatientsExamEdit = "patients.exam.edit";

    /// <summary>14. Sign exam notes.</summary>
    public const string PatientsExamSign = "patients.exam.sign";

    /// <summary>15. Issue clinical documents.</summary>
    public const string PatientsDocuments = "patients.documents";

    /// <summary>16. Order tests and imaging.</summary>
    public const string PatientsOrders = "patients.orders";

    /// <summary>17. Record results.</summary>
    public const string PatientsResults = "patients.results";

    /// <summary>18. Collect consents.</summary>
    public const string PatientsConsentsCollect = "patients.consents.collect";

    /// <summary>19. Withdraw consents.</summary>
    public const string PatientsConsentsWithdraw = "patients.consents.withdraw";

    /// <summary>20. Create treatment plans.</summary>
    public const string PatientsPlansCreate = "patients.plans.create";

    /// <summary>21. Present quotes.</summary>
    public const string PatientsPlansPresent = "patients.plans.present";

    /// <summary>22. Record patient acceptance.</summary>
    public const string PatientsPlansAccept = "patients.plans.accept";

    /// <summary>23. Record items as done.</summary>
    public const string PatientsPlansExecute = "patients.plans.execute";

    /// <summary>24. View the recall list.</summary>
    public const string PatientsRecallView = "patients.recall.view";

    /// <summary>25. Log patient contacts.</summary>
    public const string PatientsRecallLog = "patients.recall.log";

    /// <summary>26. View invoices.</summary>
    public const string MoneyView = "money.view";

    /// <summary>27. Issue invoices.</summary>
    public const string MoneyInvoice = "money.invoice";

    /// <summary>28. Collect payments.</summary>
    public const string MoneyCollect = "money.collect";

    /// <summary>29. Issue refunds.</summary>
    public const string MoneyRefund = "money.refund";

    /// <summary>30. Close shifts.</summary>
    public const string MoneyShift = "money.shift";

    /// <summary>31. View doctor statements.</summary>
    public const string MoneyStatementsView = "money.statements.view";

    /// <summary>32. Approve doctor statements.</summary>
    public const string MoneyStatementsApprove = "money.statements.approve";

    /// <summary>33. View inbox.</summary>
    public const string InboxView = "inbox.view";

    /// <summary>34. Reply to conversations.</summary>
    public const string InboxReply = "inbox.reply";

    /// <summary>35. Link conversations to patients.</summary>
    public const string InboxLink = "inbox.link";

    /// <summary>36. Block contacts.</summary>
    public const string InboxBlock = "inbox.block";

    /// <summary>37. View my tickets.</summary>
    public const string TicketsView = "tickets.view";

    /// <summary>38. View all clinic tickets.</summary>
    public const string TicketsViewAll = "tickets.viewAll";

    /// <summary>39. Open tickets.</summary>
    public const string TicketsCreate = "tickets.create";

    /// <summary>40. Reply to tickets.</summary>
    public const string TicketsReply = "tickets.reply";

    /// <summary>41. View financial reports.</summary>
    public const string ReportsFinancial = "reports.financial";

    /// <summary>42. View clinical reports.</summary>
    public const string ReportsClinical = "reports.clinical";

    /// <summary>43. View the activity log.</summary>
    public const string ReportsActivity = "reports.activity";

    /// <summary>44. View the weekly summary.</summary>
    public const string ReportsWeekly = "reports.weekly";

    /// <summary>45. View patient feedback.</summary>
    public const string ReportsFeedback = "reports.feedback";

    /// <summary>46. Follow up on patient feedback.</summary>
    public const string ReportsFeedbackFollowup = "reports.feedback.followup";

    /// <summary>47. Export reports.</summary>
    public const string ReportsExport = "reports.export";

    /// <summary>48. View users.</summary>
    public const string AdminUsersView = "admin.users.view";

    /// <summary>49. Manage users.</summary>
    public const string AdminUsersManage = "admin.users.manage";

    /// <summary>50. View roles & permissions.</summary>
    public const string AdminRolesView = "admin.roles.view";

    /// <summary>51. Manage roles & permissions.</summary>
    public const string AdminRolesManage = "admin.roles.manage";

    /// <summary>52. View licences.</summary>
    public const string AdminLicencesView = "admin.licences.view";

    /// <summary>53. Manage licences.</summary>
    public const string AdminLicencesManage = "admin.licences.manage";

    /// <summary>54. View doctors.</summary>
    public const string DataDoctorsView = "data.doctors.view";

    /// <summary>55. Manage doctors.</summary>
    public const string DataDoctorsManage = "data.doctors.manage";

    /// <summary>56. View branches.</summary>
    public const string DataBranchesView = "data.branches.view";

    /// <summary>57. Manage branches.</summary>
    public const string DataBranchesManage = "data.branches.manage";

    /// <summary>58. View services.</summary>
    public const string DataServicesView = "data.services.view";

    /// <summary>59. Manage services and prices.</summary>
    public const string DataServicesManage = "data.services.manage";

    /// <summary>60. View settings.</summary>
    public const string SettingsView = "settings.view";

    /// <summary>61. Manage working hours.</summary>
    public const string SettingsHours = "settings.hours";

    /// <summary>62. View my hours.</summary>
    public const string SettingsMyHours = "settings.myHours";

    /// <summary>63. Manage channels & booking assistant.</summary>
    public const string SettingsChannels = "settings.channels";

    /// <summary>64. Manage booking policies.</summary>
    public const string SettingsPolicies = "settings.policies";

    /// <summary>65. Manage insurance & refund policies.</summary>
    public const string SettingsInsurance = "settings.insurance";

    /// <summary>66. Manage documents.</summary>
    public const string SettingsDocuments = "settings.documents";

    /// <summary>67. Manage payment methods.</summary>
    public const string SettingsPayments = "settings.payments";

    /// <summary>68. Manage teleconsult.</summary>
    public const string SettingsTeleconsult = "settings.teleconsult";

    /// <summary>69. Manage the public booking page.</summary>
    public const string SettingsBookingPage = "settings.bookingPage";

    /// <summary>70. Manage notifications.</summary>
    public const string SettingsNotifications = "settings.notifications";

    /// <summary>71. Manage security actions.</summary>
    public const string SettingsSecurity = "settings.security";

    /// <summary>72. Manage AI features.</summary>
    public const string SettingsAi = "settings.ai";

    /// <summary>73. View the subscription.</summary>
    public const string SubscriptionView = "subscription.view";

    /// <summary>74. View subscription invoices.</summary>
    public const string SubscriptionInvoicesView = "subscription.invoices.view";

    /// <summary>75. Pay subscription invoices.</summary>
    public const string SubscriptionPay = "subscription.pay";

    /// <summary>76. Change the plan.</summary>
    public const string SubscriptionPlan = "subscription.plan";

    /// <summary>77. Buy extra units.</summary>
    public const string SubscriptionUnits = "subscription.units";

    /// <summary>78. Edit clinic details.</summary>
    public const string SubscriptionClinic = "subscription.clinic";

    /// <summary>79. Edit tax details.</summary>
    public const string SubscriptionTax = "subscription.tax";

    /// <summary>80. View consent texts.</summary>
    public const string DataConsentsView = "data.consents.view";

    /// <summary>81. Manage consent texts.</summary>
    public const string DataConsentsManage = "data.consents.manage";

    /// <summary>82. View lab cases.</summary>
    public const string PatientsLabView = "patients.lab.view";

    /// <summary>83. Manage lab cases.</summary>
    public const string PatientsLabManage = "patients.lab.manage";
}
