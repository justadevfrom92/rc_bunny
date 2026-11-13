import { Route, Routes } from 'react-router';
import Record from '../Record';
import AppShell, { type Menu } from '../../Sidebar';
import { Campaign, Campaigns, Dashboard, Heatmap } from './Campaigns';
import { PdfMade, PdfPartForm, PdfParts, PdfTemplateForm, PdfTemplates, PdfsMade } from './Pdfs';
import { AnswerSetForm, AnswerSets, Bank, BankForm, Response, Responses, Results, SurveyBuilder, SurveyCategories, SurveyCategoryForm, SurveyEmails, Surveys } from './Surveys';
import { Categories, CategoryForm, Email, Overview, PartForm, Parts, Sent, SuppressionForm, Suppressions, TemplateForm, TemplateTest, Templates } from './Emails';

// ebunny's config/admin.php → rodeo menu
const menu: Menu = [
  { heading: 'Marketing', links: [['Dashboard', ''], ['Campaigns', 'campaigns']] },
  { heading: 'Emails', links: [['Overview', 'emails'], ['Sent Emails', 'emails/sent'], ['Templates', 'templates'], ['New Template', 'templates/new'], ['Headers & Footers', 'headers-footers'],
    ['Categories', 'emails/categories'], ['Suppression List', 'emails/suppressions']] },
  { heading: 'PDFs', links: [['PDF Templates', 'pdfs'], ['New PDF Template', 'pdfs/new'], ['PDF Headers & Footers', 'pdf-headers-footers'], ['PDFs Made', 'pdfs/made']] },
  { heading: 'Surveys', links: [['All Surveys', 'surveys'], ['Survey Emails', 'survey-emails'], ['New Survey', 'surveys/new'], ['Responses', 'surveys/responses'],
    ['Question Bank', 'surveys/bank'], ['Answer Sets', 'surveys/answer-sets'], ['Categories', 'surveys/categories']] },
];

export default function Rodeo() {
  return (
    <AppShell app="rodeo" menu={menu}>
      <Routes>
        <Route index element={<Dashboard />} />
        <Route path="campaigns" element={<Campaigns />} />
        <Route path="campaigns/new" element={<Campaign key="new" />} />
        <Route path="campaigns/:id" element={<Campaign />} />
        <Route path="campaigns/:id/heatmap" element={<Heatmap kind="campaigns" />} />
        <Route path="templates/:id/heatmap" element={<Heatmap kind="templates" />} />
        <Route path="emails" element={<Overview />} />
        <Route path="emails/sent" element={<Sent />} />
        <Route path="emails/sent/:id" element={<Email />} />
        <Route path="emails/categories" element={<Categories />} />
        <Route path="emails/categories/new" element={<CategoryForm key="new" />} />
        <Route path="emails/categories/:id/edit" element={<CategoryForm />} />
        <Route path="emails/suppressions" element={<Suppressions />} />
        <Route path="emails/suppressions/new" element={<SuppressionForm />} />
        <Route path="templates" element={<Templates />} />
        <Route path="templates/new" element={<TemplateForm />} />
        <Route path="templates/:id" element={<TemplateForm />} />
        <Route path="templates/:id/test" element={<TemplateTest />} />
        <Route path="headers-footers" element={<Parts />} />
        <Route path="headers-footers/new" element={<PartForm />} />
        <Route path="headers-footers/:id" element={<PartForm />} />
        <Route path="pdfs" element={<PdfTemplates />} />
        <Route path="pdfs/new" element={<PdfTemplateForm key="new" />} />
        <Route path="pdfs/made" element={<PdfsMade />} />
        <Route path="pdfs/made/:id" element={<PdfMade />} />
        <Route path="pdfs/:id" element={<PdfTemplateForm />} />
        <Route path="pdf-headers-footers" element={<PdfParts />} />
        <Route path="pdf-headers-footers/new" element={<PdfPartForm key="new" />} />
        <Route path="pdf-headers-footers/:id" element={<PdfPartForm />} />
        <Route path="surveys" element={<Surveys />} />
        <Route path="surveys/new" element={<SurveyBuilder key="new" />} />
        <Route path="surveys/responses" element={<Responses />} />
        <Route path="surveys/responses/:id" element={<Response />} />
        <Route path="surveys/bank" element={<Bank />} />
        <Route path="surveys/bank/new" element={<BankForm key="new" />} />
        <Route path="surveys/bank/:id/edit" element={<BankForm />} />
        <Route path="surveys/answer-sets" element={<AnswerSets />} />
        <Route path="surveys/answer-sets/new" element={<AnswerSetForm key="new" />} />
        <Route path="surveys/answer-sets/:id/edit" element={<AnswerSetForm />} />
        <Route path="surveys/categories" element={<SurveyCategories />} />
        <Route path="surveys/categories/new" element={<SurveyCategoryForm key="new" />} />
        <Route path="surveys/categories/:id/edit" element={<SurveyCategoryForm />} />
        <Route path="surveys/:id" element={<SurveyBuilder />} />
        <Route path="surveys/:id/results" element={<Results />} />
        <Route path="survey-emails" element={<SurveyEmails />} />
        <Route path="records/:type/:id" element={<Record />} />
      </Routes>
    </AppShell>
  );
}
