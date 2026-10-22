/** Shapes of the admin API (see backend Services/*Dto). */

export type Lang = 'ar' | 'en';
export interface Bilingual { ar: string; en: string }
export type Role = 'Admin' | 'Editor';

export interface Me { id: string; email: string; displayName: string; role: Role; preferredLanguage: Lang }

export interface Paged<T> { items: T[]; page: number; pageSize: number; total: number; totalPages: number }

export type FieldType = 'text' | 'textArea' | 'url' | 'media' | 'select' | 'multiSelect' | 'icon' | 'bool' | 'number' | 'list' | 'days' | 'time';
export interface OptionDef { value: string; label: Bilingual }
export interface FieldDef {
  key: string;
  type: FieldType;
  label: Bilingual;
  help: Bilingual | null;
  localized: boolean;
  required: boolean;
  maxLength: number;
  format: 'none' | 'email' | 'phone' | 'absoluteUrl';
  options: OptionDef[] | null;
  itemFields: FieldDef[] | null;
  itemLabel: string | null;
  minItems: number;
  maxItems: number;
  min: number | null;
  max: number | null;
  group: string | null;
  private: boolean;
  allowDrawings: boolean;
}
export interface GroupDef { key: string; label: Bilingual }
export interface SectionSchema {
  key: string;
  name: Bilingual;
  description: Bilingual;
  anchor: string | null;
  pinned: boolean;
  canHide: boolean;
  groups: GroupDef[];
  fields: FieldDef[];
}
export interface Schemas { sections: SectionSchema[]; settings: SectionSchema; news: { blocks: string[]; maxBlocks: number; maxText: number } }

export interface SectionSummary {
  key: string;
  name: Bilingual;
  description: Bilingual;
  anchor: string | null;
  sortOrder: number;
  isVisible: boolean;
  pinned: boolean;
  canHide: boolean;
  isPublished: boolean;
  hasUnpublishedChanges: boolean;
  draftUpdatedAt: string;
  publishedAt: string | null;
  missingTranslations: number;
}
export type Json = null | boolean | number | string | Json[] | { [key: string]: Json };
export type JsonObject = { [key: string]: Json };
export interface SectionDetail {
  section: SectionSummary;
  draft: JsonObject;
  published: JsonObject | null;
  concurrencyStamp: string;
  missingTranslations: string[];
  draftUpdatedBy: string | null;
  publishedBy: string | null;
}

export interface SettingsDetail { data: JsonObject; concurrencyStamp: string; updatedAt: string; missingTranslations: string[] }

export interface Picture { id: number; url: string; type: string; width: number; height: number; alt: string; sources: { type: string; srcset: string }[] }
export interface MediaItem {
  id: number;
  kind: 'upload' | 'bundled';
  url: string;
  contentType: string;
  fileName: string;
  sizeBytes: number;
  width: number;
  height: number;
  altAr: string;
  altEn: string;
  isDrawing: boolean;
  picture: Picture;
  createdAt: string;
  updatedAt: string;
}
export interface MediaUsage { area: 'section' | 'settings' | 'news'; key: string; labelAr: string; labelEn: string }

export type NewsStatus = 'draft' | 'published' | 'scheduled';
export interface CategoryRef { id: number; slug: string; nameAr: string; nameEn: string }
export interface Category extends CategoryRef { sortOrder: number; articleCount: number }
export interface NewsListItem {
  id: number;
  slug: string;
  titleAr: string;
  titleEn: string;
  status: NewsStatus;
  publishedAt: string | null;
  isFeatured: boolean;
  priority: number;
  category: CategoryRef | null;
  imageUrl: string | null;
  updatedAt: string;
  hasArabic: boolean;
  hasEnglish: boolean;
}
export type Block =
  | { type: 'paragraph'; text: string }
  | { type: 'heading'; text: string; level: 2 | 3 }
  | { type: 'list'; style: 'bullet' | 'number'; items: string[] }
  | { type: 'quote'; text: string; cite: string }
  | { type: 'image'; mediaId: number | null; caption: string };
export interface NewsDetail {
  id: number;
  slug: string;
  titleAr: string;
  titleEn: string;
  summaryAr: string;
  summaryEn: string;
  bodyAr: Block[];
  bodyEn: Block[];
  metaTitleAr: string;
  metaTitleEn: string;
  metaDescriptionAr: string;
  metaDescriptionEn: string;
  featuredImageId: number | null;
  featuredImage: Picture | null;
  categoryId: number | null;
  status: NewsStatus;
  publishedAt: string | null;
  isFeatured: boolean;
  priority: number;
  createdAt: string;
  updatedAt: string;
  concurrencyStamp: string;
}

export type SubmissionStatus = 'new' | 'in_progress' | 'done' | 'archived';
/** A message sent from the Contact us form. */
export interface Submission {
  id: number;
  name: string;
  phone: string;
  /** Empty when the visitor left it out (it is optional). */
  email: string;
  message: string;
  language: Lang;
  status: SubmissionStatus;
  isRead: boolean;
  adminNotes: string;
  createdAt: string;
  updatedAt: string;
}

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  isActive: boolean;
  isLockedOut: boolean;
  preferredLanguage: Lang;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AuditEntry { id: number; at: string; userEmail: string; action: string; entityType: string; entityId: string; summary: string; ipAddress: string | null }

export interface Dashboard {
  sections: { total: number; visible: number; hidden: number; withUnpublishedChanges: number; missingTranslations: number };
  news: { total: number; published: number; scheduled: number; drafts: number; featured: number };
  messages: { total: number; new: number; unread: number; last7Days: number; last30Days: number };
  media: { total: number; uploads: number; uploadBytes: number };
  requestsPerDay: { day: string; count: number }[];
  recentMessages: Submission[];
  recentActivity: AuditEntry[] | null;
  lastPublishedAt: string | null;
}
