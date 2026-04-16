# Epic - Unified Patient Access & Clinical Intelligence Platform

## Epic Summary Table

| Epic ID | Epic Title | Mapped Requirement IDs |
|---------|------------|------------------------|
| EP-TECH | Project Foundation & Development Infrastructure | FR-019, FR-020, NFR-014, NFR-015, TR-001, TR-002, TR-003, TR-004, TR-005, TR-008, TR-011, TR-016, TR-024 |
| EP-DATA | Core Data Layer & Entity Persistence | DR-001, DR-002, DR-003, DR-004, DR-005, DR-006, DR-007, DR-008, DR-009, DR-010, DR-011, DR-017 |
| EP-001 | Patient Self-Service Booking & Intake | FR-002, FR-003, FR-004, FR-005, FR-006, FR-007, FR-009, TR-012, TR-013, TR-014, AIR-002 |
| EP-002 | Staff Walk-In, Queue & Arrival Management | FR-008, TR-009, NFR-001, NFR-008, UXR-002 |
| EP-003 | Clinical Document Upload & AI Extraction Pipeline | FR-010, FR-011, AIR-001, AIR-006, AIR-007, AIR-R01, AIR-R02, AIR-R03, AIR-R04, TR-015, DR-016, NFR-002 |
| EP-004 | Clinical Verification, Conflict Resolution & Code Review | FR-012, FR-013, FR-014, AIR-003, AIR-004, AIR-005, AIR-Q01, AIR-Q05, NFR-009, NFR-010, DR-018 |
| EP-005 | User Access Control & Account Management | FR-001, FR-015, FR-017, NFR-004, NFR-005, TR-010 |
| EP-006 | Security, Compliance & Audit Controls | FR-016, NFR-003, NFR-007, NFR-013, DR-012, DR-013, DR-014, DR-015, TR-018, TR-022, AIR-S01, AIR-S02 |
| EP-007 | AI Infrastructure & Gateway Services | AIR-S03, AIR-S04, AIR-O01, AIR-O02, AIR-O03, AIR-O04, AIR-Q02, AIR-Q03, AIR-Q04, TR-006, TR-007, TR-025 |
| EP-008 | Performance, Reliability & Operational Monitoring | FR-018, NFR-006, NFR-011, NFR-012, NFR-016, NFR-017, NFR-018, TR-017, TR-019, TR-020, TR-021, TR-023 |
| EP-009-I | Accessibility & Responsive Design Foundation | UXR-101, UXR-102, UXR-103, UXR-104, UXR-105, UXR-201, UXR-202, UXR-203, UXR-301, UXR-302 |
| EP-009-II | Interaction Patterns, Feedback & Error Handling | UXR-001, UXR-003, UXR-303, UXR-401, UXR-402, UXR-403, UXR-404, UXR-501, UXR-502, UXR-503, UXR-504 |

## Epic Description

### EP-TECH: Project Foundation & Development Infrastructure

**Business Value**: Enables all subsequent development by establishing the greenfield project foundation, technology stack scaffolding, CI/CD pipeline, and shared infrastructure services required by every feature epic.

**Description**: Bootstrap the greenfield repository with the BRD-approved technology stack: React 18 frontend SPA, .NET 8 backend API, PostgreSQL 15 with pgvector extension, and Upstash Redis caching layer. Establish the modular monolith architecture with patient-access and clinical-intelligence bounded contexts using layered pattern within each module. Configure Entity Framework Core 8 with code-first migration tooling, REST API with Swagger/OpenAPI documentation, GitHub Actions CI/CD pipeline, and Docker containers for development environment consistency. Ensure native Windows Services/IIS deployment support and strictly free/open-source technology stacks for all auxiliary processing.

**UI Impact**: Yes

**Screen References**: SCR-024 (Login shell), SCR-025 (Header/Navigation shell)

**Key Deliverables**:

- React 18 project scaffolding with Material-UI component library and state management (React Query + Zustand)
- .NET 8 Web API project structure with modular monolith bounded contexts (patient-access, clinical-intelligence, admin)
- PostgreSQL 15 database initialization with pgvector extension enabled
- Upstash Redis integration for session state and query caching
- Entity Framework Core 8 configuration with code-first migration workflow
- Swagger/OpenAPI documentation endpoint
- GitHub Actions CI/CD pipeline with build, test, and deployment stages
- Docker Compose configuration for local development environment
- IIS deployment configuration with web deploy packages
- Shared project structure with layered architecture (presentation, application, domain, data) per bounded context

**Dependent EPICs**:

- None

---

### EP-DATA: Core Data Layer & Entity Persistence

**Business Value**: Enables data operations for all feature epics by establishing the 10 core domain entities, relationships, referential integrity constraints, and data management patterns required for booking, clinical intelligence, and administrative workflows.

**Description**: Implement the complete domain entity model from the ERD in models.md using Entity Framework Core code-first approach. Create entity configurations for Patient, Appointment, IntakeResponse, ClinicalDocument, ExtractedFact, PatientView360, CodeSuggestion, AuditLog, Staff, and Admin entities with all specified attributes, data types, and constraints. Enforce referential integrity across all entity relationships with appropriate cascading rules. Implement soft delete pattern for patient and appointment records to preserve audit trail while supporting deletion requests. Configure seed data and migration scripts for development and testing environments.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:

- Patient entity with email unique key, demographics, insurance information, and relationships to appointments, intake responses, documents, and 360-view
- Appointment entity with status enumeration (booked, arrived, completed, cancelled, no-show), optional preferred-slot self-reference for swap watchlist
- IntakeResponse entity with mode indicator and JSONB answers payload
- ClinicalDocument entity with file storage reference, extraction status tracking, and encounter association
- ExtractedFact entity with fact type enumeration, confidence score, and source segment reference for citation
- PatientView360 entity with JSONB consolidated facts, conflict flags array, and verification status
- CodeSuggestion entity with medical code value, evidence fact ID array, and staff review tracking
- AuditLog entity as immutable append-only table with actor, action, target, and JSONB payload
- Staff entity with role enumeration and permissions bitfield
- Admin entity with access control privileges
- Referential integrity constraints across all relationships
- Soft delete pattern implementation for patient and appointment records
- Seed data and EF Core migration scripts

**Dependent EPICs**:

- EP-TECH - Foundational - Requires project scaffolding, EF Core configuration, and PostgreSQL setup

---

### EP-001: Patient Self-Service Booking & Intake

**Business Value**: Delivers the highest-value patient-facing workflow enabling self-service appointment booking, intake completion, and automated scheduling artifacts. Directly reduces booking friction and no-show rates while eliminating staff intervention for standard appointments.

**Description**: Implement the complete patient booking flow from availability search through booking confirmation with automated notifications. Patients search available slots, select an appointment, provide contact and insurance details, and complete intake through either AI-assisted conversational mode or manual form with seamless mode switching. The system performs insurance soft validation against internal dummy records, computes rule-based no-show risk scores, and creates the appointment transaction. Post-booking, the system sends SMS/email reminders via Twilio and SendGrid free tiers, creates Google and Outlook calendar events via OAuth 2.0 APIs, and generates PDF appointment confirmations. Patients can also select a preferred unavailable slot for automatic swap watchlist enrollment, with the system monitoring availability and executing atomic slot reassignment when the preferred slot opens.

**UI Impact**: Yes

**Screen References**: SCR-001 (Availability Search), SCR-002 (Slot Selection), SCR-003 (Patient Details Form), SCR-004 (Manual Intake Form), SCR-005 (Conversational Intake), SCR-006 (Booking Confirmation), SCR-007 (Booking Error), SCR-008 (My Appointments), SCR-009 (Preferred Slot Selection)

**Key Deliverables**:

- Appointment availability search API with Redis-cached slot inventory (60s TTL)
- Appointment booking transaction with patient registration and insurance soft validation
- AI-assisted conversational intake using Azure OpenAI natural language understanding
- Manual intake form with mode switching and answer preservation
- Rule-based no-show risk scoring calculator using scheduling and patient-response signals
- Preferred slot selection and swap watchlist enrollment
- Automatic slot swap with atomic reassignment, original slot release, and patient notification
- Google Calendar API integration via OAuth 2.0 for calendar event creation
- Outlook Calendar API integration via Microsoft Graph for calendar event creation
- Twilio SMS and SendGrid Email integration for automated reminders
- PDF appointment confirmation generation using PDFSharp
- Booking confirmation screen with PDF download and calendar sync options

**Dependent EPICs**:

- EP-TECH - Foundational - Requires React frontend, .NET API, and infrastructure services
- EP-DATA - Foundational - Requires Patient, Appointment, and IntakeResponse entities

---

### EP-002: Staff Walk-In, Queue & Arrival Management

**Business Value**: Enables staff-controlled same-day operations including walk-in booking, queue management, and arrival tracking. Ensures patients cannot self-check in through any channel, maintaining staff operational control over same-day clinical flow.

**Description**: Implement staff-only workflows for walk-in patient management, same-day queue handling, and arrival status updates. Staff can search for existing patients or create new patient accounts during walk-in intake. The system places walk-in appointments into a same-day operational queue with position tracking. Staff can reorder queue entries and mark patients as arrived when present. All queue state changes are cached in Redis for real-time consistency, and breadcrumb navigation supports staff workflow depth orientation. Background job processing via Hangfire supports queue monitoring and slot availability polling for swap watchlist. System performance targets 2-second response time at the 95th percentile and supports horizontal scaling for concurrent users per practice.

**UI Impact**: Yes

**Screen References**: SCR-010 (Staff Dashboard), SCR-011 (Walk-In Booking), SCR-012 (Same-Day Queue), SCR-013 (Patient Arrival Marking)

**Key Deliverables**:

- Staff-only walk-in booking API with patient search and inline account creation
- Same-day queue management with position tracking and reordering
- Patient arrival marking with status transitions (waiting → arrived → in-room → completed)
- Real-time queue state with Redis caching (30s TTL) and cache invalidation on updates
- Hangfire background job integration for queue monitoring and swap watchlist polling
- Staff dashboard with summary cards (walk-ins today, queue length, verification pending)
- Breadcrumb navigation for staff workflow depth orientation

**Dependent EPICs**:

- EP-TECH - Foundational - Requires .NET API, Redis caching, and Hangfire setup
- EP-DATA - Foundational - Requires Patient and Appointment entities

---

### EP-003: Clinical Document Upload & AI Extraction Pipeline

**Business Value**: Transforms manual 20+ minute document review into an automated extraction pipeline, enabling staff chart-prep workflows to target approximately 2 minutes per patient. Creates the foundation for trust-first clinical intelligence by preserving source documents and extracting structured clinical facts with source citations.

**Description**: Implement the document upload and AI extraction pipeline. Patients upload historical or post-visit documents (PDF) which are validated, stored, and associated with patient and encounter context. A background extraction job processes uploaded documents through the RAG pipeline: documents are parsed to text, chunked into 512-token segments with 25% overlap, embedded using text-embedding-3-small model, and stored in pgvector for similarity search. The RAG extraction process retrieves top-5 chunks via cosine similarity (threshold 0.7+), re-ranks by semantic relevance, and assembles context within a 3,000-token window. GPT-4 Turbo extracts structured clinical facts (vitals, medications, history, diagnoses, procedures) with confidence scores and character-level source segment references. Facts with confidence below 70% are flagged for staff manual review. The unified patient view is updated with traceable new evidence.

**UI Impact**: Yes

**Screen References**: SCR-014 (Document Upload), SCR-015 (Document List)

**Key Deliverables**:

- Document upload API with file type/size validation and patient/encounter association
- Document storage with original file preservation and file reference tracking
- Text extraction from PDF documents
- Document chunking service (512 tokens, 25% overlap)
- Embedding generation using text-embedding-3-small model with pgvector storage
- pgvector similarity search with cosine distance metric
- RAG retrieval with top-5 chunks, 0.7+ cosine threshold, and semantic re-ranking
- Context window assembly limited to 3,000 tokens
- GPT-4 Turbo clinical fact extraction with structured output (vitals, medications, history, diagnoses, procedures)
- Confidence scoring with automatic fallback to manual review below 70% threshold
- Source citation generation with character-level offset and length for citation verification
- Background extraction job processing via Hangfire
- Document processing status tracking (queued, processing, completed, manual_review)

**Dependent EPICs**:

- EP-TECH - Foundational - Requires .NET API, PostgreSQL, and Hangfire infrastructure
- EP-DATA - Foundational - Requires ClinicalDocument, ExtractedFact, and VectorEmbedding entities

---

### EP-004: Clinical Verification, Conflict Resolution & Code Review

**Business Value**: Closes the trust loop between AI-generated outputs and clinical use by requiring mandatory staff verification. Enables the 2-minute chart prep target and >98% AI-human agreement rate through source-linked evidence, conflict highlighting, and human-confirmed code suggestions.

**Description**: Implement the staff-facing clinical verification workflow. Staff opens the 360-degree patient view assembled from intake data, uploaded documents, and extracted facts. The system de-duplicates overlapping facts using semantic similarity and entity resolution, presenting each consolidated value with contributing sources. Clinically meaningful conflicts (contradictory medications, history details) are detected and highlighted as mandatory review items requiring staff acknowledgement. ICD-10 and CPT code suggestions are generated from aggregated patient data with supporting evidence from extracted facts. Staff reviews, confirms, or rejects each code with audit trail capture. The patient summary uses optimistic concurrency control to prevent conflict resolution race conditions, with version tracking for concurrent update safety.

**UI Impact**: Yes

**Screen References**: SCR-016 (Patient Chart Review), SCR-017 (360-Degree Patient View), SCR-018 (Conflict Resolution), SCR-019 (Code Verification), SCR-020 (Verification Complete)

**Key Deliverables**:

- 360-degree patient view assembly with de-duplication and source traceability
- Consolidated facts display with JSONB storage and contributing source references
- Conflict detection engine for clinically meaningful contradictions across aggregated sources
- Conflict resolution workflow with accept/reject/manual-override options and audit justification
- ICD-10 and CPT code suggestion generation from aggregated patient data
- Code suggestion display with evidence breadcrumbs and supporting fact links
- Staff review and confirmation workflow with accept/reject/modify actions
- Verification status tracking with optimistic concurrency control (version increment)
- Healthcare semantic color coding for fact categories (vitals pink, medications orange, history brown, diagnoses purple, procedures teal)
- Chart prep timer tracking to validate 2-minute target
- AI-human agreement rate tracking for extracted facts and suggested codes

**Dependent EPICs**:

- EP-TECH - Foundational - Requires React frontend and .NET API infrastructure
- EP-DATA - Foundational - Requires PatientView360, ExtractedFact, and CodeSuggestion entities

---

### EP-005: User Access Control & Account Management

**Business Value**: Establishes the role-based access foundation ensuring patients, staff, and admins interact only with authorized workflows. Enables secure session handling and administrative governance over user lifecycle.

**Description**: Implement role-based access control using ASP.NET Core Identity for three user roles: patient (self-service booking and document actions), staff (operational and clinical review actions), and admin (user access and governance settings). Admins can create, update, disable, and role-assign user accounts. Authorized staff can create patient accounts as part of walk-in or call-center booking. The system enforces strict separation between role capabilities, protects sessions with 15-minute inactivity timeout with forced re-authentication, and applies secure PHI handling rules across storage, transmission, and access workflows.

**UI Impact**: Yes

**Screen References**: SCR-021 (User Management), SCR-022 (Create/Edit User), SCR-023 (Role Assignment), SCR-024 (Login)

**Key Deliverables**:

- ASP.NET Core Identity configuration with patient, staff, and admin roles
- Custom authorization policies enforcing strict role separation
- Admin user management API (create, update, disable, role-assign)
- Staff-initiated patient account creation for walk-in workflows
- 15-minute session inactivity timeout with forced re-authentication
- Session timeout warning modal at 14-minute mark
- Login screen with email/password authentication
- User management interface with search, filter, and bulk actions
- Role assignment modal with permissions bitfield configuration
- Inline form validation for user management forms

**Dependent EPICs**:

- EP-TECH - Foundational - Requires .NET API and ASP.NET Core Identity setup
- EP-DATA - Foundational - Requires Staff, Admin, and Patient entities

---

### EP-006: Security, Compliance & Audit Controls

**Business Value**: Satisfies HIPAA compliance requirements and establishes trust through immutable audit logging, PHI encryption, and AI prompt safety controls. Ensures the platform meets regulatory obligations for healthcare data handling, transmission, and storage.

**Description**: Implement comprehensive security and compliance controls. Create an immutable audit log for all patient, staff, and admin actions affecting appointments, intake, documents, extracted data, code confirmations, and access administration. Encrypt all PHI data at rest using AES-256 via .NET Data Protection API with column-level encryption for sensitive fields (Patient demographics, IntakeResponse answers, ClinicalDocument file references, ExtractedFact values, PatientView360 consolidated facts). Enforce TLS 1.2+ for all data in transit. Configure structured logging with Serilog for audit trail persistence. Implement AI safety controls including PII redaction from non-clinical AI prompts and document access control lists during RAG retrieval to prevent cross-patient information leakage. Establish backup strategy with daily incremental and weekly full backups with 7-year retention. Support zero-downtime database migrations using blue-green deployment patterns.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:

- Immutable audit log implementation with append-only AuditLog table
- Audit logging interceptor for EF Core capturing all data mutations
- AES-256 encryption at rest via .NET Data Protection API
- Column-level PHI encryption for Patient, IntakeResponse, ClinicalDocument, ExtractedFact, PatientView360 fields
- TLS 1.2+ enforcement for all data in transit
- Serilog structured logging with audit trail sinks
- Audit log indefinite retention to satisfy HIPAA requirements
- Daily incremental and weekly full backup configuration with 7-year retention
- Zero-downtime migration strategy using blue-green deployment patterns
- PII redaction from AI prompts for non-clinical conversational intake
- Document access control list enforcement during RAG retrieval to prevent cross-patient leakage
- 100% HIPAA compliance for data handling, transmission, storage, and audit logging

**Dependent EPICs**:

- EP-TECH - Foundational - Requires .NET API, Data Protection API, and database infrastructure

---

### EP-007: AI Infrastructure & Gateway Services

**Business Value**: Provides the foundational AI platform services that power conversational intake, clinical extraction, and code suggestions. Ensures AI operations are cost-controlled, auditable, resilient to provider failures, and safe from harmful content generation.

**Description**: Implement the custom AI gateway middleware and supporting AI infrastructure services. Configure Azure OpenAI Service with signed HIPAA BAA for GPT-4 Turbo LLM inference and text-embedding-3-small embedding generation. Build the custom AI gateway middleware for token budget enforcement (8,000 tokens per extraction request), request/response audit logging to satisfy HIPAA requirements, and circuit breaker implementation with automatic degradation to manual-review-only mode during provider failures. Implement content filtering using Azure OpenAI Content Safety to block harmful or inappropriate AI-generated responses. Support AI model version rollback within 1 hour for regression mitigation. Cache embedding vectors to avoid re-computation on retrieval queries. Enforce p95 latency targets of 3 seconds for conversational intake and 30 seconds for document extraction. Validate structured output schema at 98%+ for extracted facts. Configure feature flags for gradual AI feature rollout and A/B testing.

**UI Impact**: No

**Screen References**: N/A

**Key Deliverables**:

- Azure OpenAI Service client configuration with HIPAA BAA validation
- Custom AI gateway middleware for token budget enforcement (8,000 tokens/request)
- AI request/response audit logging to HIPAA-compliant audit table
- Circuit breaker for AI model provider failures with automatic degradation to manual-review mode
- Content filtering integration with Azure OpenAI Content Safety
- AI model version rollback capability within 1 hour
- Embedding vector caching to avoid re-computation
- p95 latency enforcement: 3s for conversational intake, 30s for document extraction
- Structured output schema validation at 98%+ for extracted facts
- Feature flag configuration for gradual AI feature rollout and A/B testing
- Retry logic and fallback strategies for Azure OpenAI rate limits

**Dependent EPICs**:

- EP-TECH - Foundational - Requires .NET API infrastructure and middleware pipeline
- EP-DATA - Foundational - Requires AuditLog entity for AI prompt/response logging

---

### EP-008: Performance, Reliability & Operational Monitoring

**Business Value**: Ensures the platform meets its 99.9% uptime target, operational transparency goals, and performance benchmarks. Provides measurable dashboards for booking volumes, no-show outcomes, AI agreement rates, and critical conflicts to validate phase-1 success criteria.

**Description**: Implement operational metrics, monitoring infrastructure, and reliability patterns. Expose operational reporting for booking volumes, completed dashboards, no-show risk distribution, no-show outcomes, AI-human agreement on extracted data and coding, and critical conflicts identified. Configure Azure Application Insights (free tier) for application performance monitoring with custom metrics tracking. Implement health check endpoints for automated uptime monitoring to satisfy the 99.9% availability target. Support zero-downtime database schema migrations to preserve availability. Implement circuit breaker patterns for all external service dependencies with automatic fallback to degraded-mode operation. Provide point-in-time recovery capability with RPO of 1 hour and RTO of 4 hours. Configure database connection pooling (min 10, max 100 connections). Implement retry logic with exponential backoff for transient external API failures. Set up Playwright end-to-end testing for critical booking and clinical review workflows.

**UI Impact**: Yes

**Screen References**: SCR-028 (Operational Metrics Dashboard)

**Key Deliverables**:

- Operational metrics dashboard with booking volumes, no-show rates, AI agreement rates, and conflict counts
- Azure Application Insights integration for APM with custom metrics
- Health check endpoints for uptime monitoring (99.9% availability target)
- Circuit breaker patterns for external service dependencies with automatic fallback
- Point-in-time recovery capability (RPO 1 hour, RTO 4 hours)
- Zero-downtime database schema migration support
- Database connection pooling configuration (min 10, max 100 connections)
- Retry logic with exponential backoff for transient external API failures
- Playwright E2E test suite for critical patient booking and staff clinical review workflows
- AI conversational intake p95 latency monitoring (3-second target)
- Operational reporting API for no-show risk distribution and AI-human agreement tracking
- Docker container configuration for development environment consistency

**Dependent EPICs**:

- EP-TECH - Foundational - Requires project infrastructure, CI/CD pipeline, and monitoring endpoints

---

### EP-009-I: Accessibility & Responsive Design Foundation

**Business Value**: Ensures the platform is accessible to all users regardless of ability, device, or screen size. Establishes WCAG 2.2 AA compliance and responsive design patterns that apply across every screen, building inclusivity into the platform foundation.

**Description**: Implement foundational accessibility and responsive design patterns across the entire application. Ensure WCAG 2.2 AA compliance with screen reader navigation support via semantic HTML and ARIA labels, full keyboard navigation with visible focus indicators (2px solid primary.500 outline), and minimum color contrast ratios (4.5:1 for text, 3:1 for UI components). Configure responsive breakpoints for mobile (320px+), tablet (768px+), and desktop (1024px+) with minimum 44x44px touch targets on mobile and tablet. Implement adaptive navigation patterns: bottom navigation for mobile, persistent sidebar for desktop. Establish the Material-UI design system with healthcare-appropriate color palette and consistent spacing based on 8px grid system.

**UI Impact**: Yes

**Screen References**: All screens (SCR-001 through SCR-028)

**Key Deliverables**:

- WCAG 2.2 AA compliance audit and remediation across all screens
- Semantic HTML structure with ARIA labels for all interactive elements
- Full keyboard navigation with tab order audit and visible focus indicators
- Color contrast validation (4.5:1 text, 3:1 UI components) using WebAIM tooling
- Responsive breakpoint configuration (320px mobile, 768px tablet, 1024px desktop)
- Minimum 44x44px touch targets on mobile and tablet viewports
- Adaptive navigation implementation: bottom nav (mobile), sidebar (desktop)
- Material-UI theme configuration with healthcare-appropriate color palette
- 8px grid spacing system enforcement across all layouts
- Screen reader audit pass for all interactive elements

**Dependent EPICs**:

- EP-TECH - Foundational - Requires React frontend with Material-UI setup

---

### EP-009-II: Interaction Patterns, Feedback & Error Handling

**Business Value**: Delivers polished user interaction patterns that build confidence and reduce task abandonment. Ensures users receive immediate feedback on actions, clear guidance through complex workflows, and actionable recovery paths when errors occur.

**Description**: Implement cross-cutting interaction patterns, feedback systems, and error handling across the application. Provide navigation to any feature in maximum 3 clicks from authenticated dashboard. Display inline guidance for complex workflows including conversational intake, conflict resolution, and code verification. Implement loading feedback within 200ms of user action, success/error toast notifications for all state-changing actions, and progress indicators for multi-step workflows (booking 3-step, verification 4-step). Enable optimistic UI updates with rollback on failure for slot selection and queue actions. Display actionable error messages with recovery paths and inline validation feedback on form fields triggered on blur. Handle network errors gracefully with retry options and offline state indicators. Apply healthcare-appropriate semantic colors for clinical fact categories (vitals pink, medications orange, history brown, diagnoses purple, procedures teal).

**UI Impact**: Yes

**Screen References**: All screens, with emphasis on SCR-001 through SCR-006 (booking flow), SCR-016 through SCR-020 (verification flow), SCR-026 (User Profile), SCR-027 (Settings)

**Key Deliverables**:

- 3-click navigation audit and optimization for all features from authenticated dashboard
- Inline guidance components (help text, tooltips) for complex workflows
- Healthcare semantic color coding for clinical fact categories
- Loading feedback system with spinner display within 200ms of user action
- Success/error toast notification system for all state-changing mutations
- Progress stepper components for multi-step workflows (booking, intake, verification)
- Optimistic UI update pattern with rollback on API conflict (409 response)
- Actionable error messages with "Try again" actions and alternative path suggestions
- Inline form validation with on-blur triggers and error text below fields
- Network error handling with retry button and offline state indicator
- Session timeout warning modal at 14-minute inactivity mark

**Dependent EPICs**:

- EP-009-I - Decomposed - This is Part II of the User Experience foundation epic, requiring accessibility and design system patterns from Part I
