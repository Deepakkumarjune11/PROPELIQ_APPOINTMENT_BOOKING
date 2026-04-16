# Architecture Design

## Project Overview
Unified Patient Access & Clinical Intelligence Platform is a greenfield healthcare application that bridges appointment scheduling with clinical data management. It serves patients booking appointments and completing intake, staff managing walk-ins and verifying AI-extracted clinical summaries, and admins governing user access. The platform reduces no-show rates through smart scheduling and cuts chart-prep time from 20+ minutes to approximately 2 minutes by automating document extraction with human-verified outputs.

## Architecture Goals
- **Architecture Goal 1**: Enforce HIPAA compliance across all data handling, transmission, and storage touchpoints
- **Architecture Goal 2**: Support 99.9% uptime (43 minutes/month downtime budget) with robust session management and failure recovery
- **Architecture Goal 3**: Enable phase-1 delivery on free/open-source hosting while maintaining migration path to Windows Services/IIS production deployment
- **Architecture Goal 4**: Minimize operational complexity through modular monolith architecture, deferring microservices until demonstrated scaling needs
- **Architecture Goal 5**: Integrate AI-assisted clinical workflows with mandatory human verification to achieve >98% agreement rate and maintain trust

## Non-Functional Requirements
- NFR-001: System MUST respond to patient booking requests within 2 seconds at the 95th percentile to maintain acceptable user experience
- NFR-002: System MUST process staff chart-prep workflows from document upload to verified 360-degree patient view in under 2 minutes per patient
- NFR-003: System MUST encrypt all PHI data at rest using AES-256 encryption and in transit using TLS 1.2 or higher
- NFR-004: System MUST enforce role-based access control with strict separation between patient self-service actions, staff operational actions, and admin governance actions
- NFR-005: System MUST terminate inactive authenticated sessions after 15 minutes and force re-authentication for subsequent access
- NFR-006: System MUST maintain 99.9% uptime measured as availability of core booking and clinical review workflows
- NFR-007: System MUST create immutable audit log entries for all patient data access, appointment changes, clinical data modifications, and code confirmations
- NFR-008: System MUST support horizontal scaling to accommodate hundreds of concurrent users per medical practice without performance degradation
- NFR-009: System MUST achieve AI-human agreement rate greater than 98% for extracted clinical facts and suggested medical codes before operational deployment
- NFR-010: System MUST maintain hallucination rate below 2% on medical fact extraction validation set
- NFR-011: System MUST respond to AI-assisted conversational intake prompts within 3 seconds at the 95th percentile
- NFR-012: System MUST enforce zero-downtime database schema migrations to preserve 99.9% availability target
- NFR-013: System MUST comply with 100% HIPAA requirements for data handling, transmission, storage, and audit logging
- NFR-014: System MUST support deployment to Windows Services or IIS hosting environments
- NFR-015: System MUST use exclusively free and open-source technology stacks for auxiliary processing, background jobs, and utility workflows
- NFR-016: System MUST implement circuit breaker patterns for external service dependencies with automatic fallback to degraded-mode operation
- NFR-017: System MUST provide point-in-time recovery capability for all patient and clinical data with RPO of 1 hour and RTO of 4 hours
- NFR-018: System MUST track and report operational metrics for no-show risk distribution, booking volumes, dashboard creation counts, AI agreement rates, and critical conflicts detected

## Data Requirements
- DR-001: System MUST define Patient entity with email as unique identifier, demographics (name, DOB, phone), insurance information (provider name, member ID, validation status), and relationship to appointments and documents
- DR-002: System MUST define Appointment entity with patient foreign key, slot datetime, status enumeration (booked, arrived, completed, cancelled, no-show), optional preferred-slot foreign key for swap watchlist, and audit trail references
- DR-003: System MUST define IntakeResponse entity with patient foreign key, mode indicator (conversational or manual), answers stored as JSONB for flexibility, and creation timestamp
- DR-004: System MUST define ClinicalDocument entity with patient foreign key, optional encounter foreign key, file storage reference (blob or URL), upload timestamp, and extraction processing status
- DR-005: System MUST define ExtractedFact entity with document foreign key, fact type enumeration (vitals, medications, history, diagnoses, procedures), extracted value, confidence score, and source segment reference for citation
- DR-006: System MUST define PatientView360 entity with patient foreign key, consolidated facts stored as JSONB, conflict flags array, verification status, and last-updated timestamp
- DR-007: System MUST define CodeSuggestion entity with patient foreign key, medical code (ICD-10 or CPT), array of evidence fact foreign keys, and staff-reviewed boolean flag
- DR-008: System MUST define AuditLog entity as immutable append-only table with actor ID, action type, target entity reference, timestamp, and payload JSONB
- DR-009: System MUST define Staff entity with role enumeration (front-desk, call-center, clinical-reviewer), permissions bitfield, and authentication credentials
- DR-010: System MUST define Admin entity with access control privileges and user management permissions
- DR-011: System MUST enforce referential integrity constraints across all entity relationships with cascading delete rules where appropriate
- DR-012: System MUST retain AuditLog records indefinitely to satisfy HIPAA compliance requirements
- DR-013: System MUST perform daily incremental backups with full weekly backups and retain backup history for 7 years
- DR-014: System MUST implement zero-downtime migration strategy using blue-green deployment or rolling update patterns
- DR-015: System MUST encrypt PHI columns (Patient.demographics, IntakeResponse.answers, ClinicalDocument.file, ExtractedFact.value, PatientView360.consolidated_facts) at rest using column-level encryption
- DR-016: System MUST store vector embeddings for RAG retrieval in PostgreSQL using pgvector extension to consolidate data infrastructure
- DR-017: System MUST implement soft delete pattern for patient and appointment records to preserve audit trail while supporting GDPR-style deletion requests
- DR-018: System MUST use optimistic concurrency control for PatientView360 updates to prevent conflict resolution race conditions

### Domain Entities
- **Patient**: Core entity representing healthcare consumers. Attributes include unique email, demographics (name, DOB, phone), insurance details (provider, member ID, validation result). Relationships: one-to-many appointments, one-to-many intake responses, one-to-many clinical documents, one-to-one 360-degree view.
- **Appointment**: Scheduled visit record. Attributes include slot datetime, status (booked/arrived/completed/cancelled/no-show), optional preferred-slot reference for swap watchlist. Relationships: belongs-to patient, optional self-reference for preferred slot.
- **Staff**: Healthcare workers and support personnel. Attributes include role (front-desk, call-center, clinical-reviewer), permissions, authentication. Relationships: one-to-many audit log entries as actor.
- **Admin**: System administrators. Attributes include access control privileges, user management permissions. Relationships: one-to-many audit log entries as actor.
- **IntakeResponse**: Patient intake form submission. Attributes include mode (conversational or manual), JSONB answers payload, timestamp. Relationships: belongs-to patient.
- **ClinicalDocument**: Uploaded medical records. Attributes include file reference, upload timestamp, extraction status. Relationships: belongs-to patient, belongs-to encounter (optional), one-to-many extracted facts.
- **ExtractedFact**: AI-extracted clinical data point. Attributes include fact type (vitals/meds/history/diagnoses/procedures), value, confidence score, source segment reference. Relationships: belongs-to document.
- **PatientView360**: De-duplicated and consolidated patient clinical summary. Attributes include JSONB consolidated facts, conflict flags array, verification status, last-updated timestamp. Relationships: belongs-to patient, references multiple extracted facts.
- **CodeSuggestion**: AI-generated ICD-10 or CPT code recommendations. Attributes include code value, evidence fact IDs, staff-reviewed flag. Relationships: belongs-to patient, references multiple extracted facts.
- **AuditLog**: Immutable compliance record. Attributes include actor ID, action type, target entity, timestamp, JSONB payload. Relationships: belongs-to actor (staff or admin).

## AI Consideration

**Status:** Applicable

AI features are present in spec.md: FR-011 [AI-CANDIDATE] for clinical document extraction, FR-003 [HYBRID] for conversational intake, FR-012 [HYBRID] for 360-degree view assembly, FR-013 [HYBRID] for conflict detection, and FR-014 [HYBRID] for code suggestions. All AI workflows follow human-in-the-loop validation pattern to maintain trust and comply with medical safety requirements.

## AI Requirements
- AIR-001: System MUST extract structured clinical facts (vitals, medications, history, diagnoses, procedures) from uploaded PDF documents using RAG pattern with source citations
- AIR-002: System MUST provide conversational intake experience using natural language understanding to guide patients through required intake questions
- AIR-003: System MUST assemble de-duplicated 360-degree patient view from multiple document sources using semantic similarity and entity resolution
- AIR-004: System MUST detect clinically meaningful conflicts across aggregated data sources (e.g., contradictory medications or history) and flag for mandatory staff review
- AIR-005: System MUST generate ICD-10 and CPT code suggestions from aggregated patient data and present supporting evidence from extracted facts
- AIR-006: System MUST link all AI-generated outputs to source document segments with character-level precision to enable citation verification
- AIR-007: System MUST implement fallback to manual staff review when AI extraction confidence score falls below 70% threshold
- AIR-Q01: System MUST maintain hallucination rate below 2% on held-out medical fact extraction validation set
- AIR-Q02: System MUST achieve p95 latency under 3 seconds for conversational intake AI responses
- AIR-Q03: System MUST achieve p95 latency under 30 seconds for full document extraction processing
- AIR-Q04: System MUST enforce output schema validity at 98% or higher for structured fact extraction
- AIR-Q05: System MUST achieve AI-human agreement rate greater than 98% for extracted clinical facts and suggested codes
- AIR-S01: System MUST redact PII from AI prompts before model invocation when processing non-clinical conversational intake
- AIR-S02: System MUST enforce document access control lists during RAG retrieval to prevent cross-patient information leakage
- AIR-S03: System MUST log all AI prompts and responses to audit table with retention period matching HIPAA requirements
- AIR-S04: System MUST implement content filtering to block unsafe or inappropriate AI-generated responses before displaying to users
- AIR-O01: System MUST enforce token budget of 8,000 tokens per extraction request to control AI inference costs
- AIR-O02: System MUST implement circuit breaker for AI model provider failures with automatic degradation to manual-review-only mode
- AIR-O03: System MUST support AI model version rollback within 1 hour to mitigate regression issues
- AIR-O04: System MUST cache embedding vectors for uploaded documents to avoid re-computation on retrieval queries
- AIR-R01: System MUST chunk clinical documents into 512-token segments with 25% overlap to preserve context across chunk boundaries
- AIR-R02: System MUST retrieve top-5 chunks with cosine similarity threshold of 0.7 or higher for RAG context assembly
- AIR-R03: System MUST re-rank retrieved chunks using semantic relevance scoring before final context window assembly
- AIR-R04: System MUST limit RAG context window to 3,000 tokens to balance grounding quality with inference latency

### AI Architecture Pattern
**Selected Pattern:** RAG (Retrieval-Augmented Generation)

**Rationale:** All AI features require grounding to source documents for trust and safety. FR-011 extraction needs source citations for clinical facts. FR-012 360-view assembly needs document provenance. FR-014 code suggestions need evidence traceability. RAG pattern satisfies these requirements by retrieving relevant document chunks before generation, enabling character-level source anchoring for human verification. HYBRID tags in spec indicate AI-assisted workflows with mandatory staff review, which aligns with RAG's strength in providing transparent evidence chains. Pure fine-tuning would lack source citations. Tool calling is insufficient for document understanding tasks.

## Architecture and Design Decisions
- **Decision 1: Modular Monolith with Bounded Contexts** - Deploy single application with patient-access and clinical-intelligence modules as separate bounded contexts within shared deployment unit. Justification: Phase-1 greenfield with single development team benefits from simplified deployment and cross-module transactions. Avoids distributed system complexity while establishing clear module boundaries for future extraction to microservices if scaling demands arise. Satisfies KISS principle and NFR-006 availability targets without service mesh overhead.
- **Decision 2: PostgreSQL + pgvector for RAG Vector Storage** - Use pgvector extension within existing PostgreSQL database for embedding storage and similarity search instead of separate vector database. Justification: Consolidates infrastructure to single database instance, eliminating separate vector DB licensing and hosting costs (satisfies NFR-015 free/OSS constraint). Reduces operational complexity and network latency. Sufficient performance for phase-1 scale (hundreds not thousands of concurrent users per NFR-008). Simplifies HIPAA compliance boundary with single data store.
- **Decision 3: Custom AI Gateway Layer** - Implement lightweight custom AI gateway for token budgeting, audit logging, and circuit breaking instead of third-party solutions like LiteLLM or Portkey. Justification: Third-party AI gateways introduce paid dependencies violating NFR-015. Custom implementation satisfies AIR-O01 token budget, AIR-S03 audit logging, and AIR-O02 circuit breaker requirements with full control over HIPAA compliance. Minimal scope reduces maintenance burden.
- **Decision 4: React Frontend + .NET Backend** - Select React for frontend SPA and .NET 8 for backend API from BRD-approved technology set. Justification: React ecosystem provides best free hosting support (Netlify/Vercel native deployment) and largest OSS component library. .NET provides native Windows Services/IIS deployment support (NFR-014), mature Azure OpenAI SDK for HIPAA-compliant AI integration, and strong healthcare OSS ecosystem for HL7 FHIR if needed. Stack coherence improves team productivity.
- **Decision 5: Layered Architecture within Modules** - Apply layered pattern (presentation, application, domain, data) within each bounded context module. Justification: Clear separation of concerns supports testability and maintainability. Well-understood pattern reduces onboarding friction. Domain layer isolation enables business rule testing without infrastructure dependencies. Avoids hexagonal architecture complexity for deterministic workflows and focused AI integration points.
- **Decision 6: Azure OpenAI with BAA for AI Model Provider** - Use Azure OpenAI Service as LLM provider with signed HIPAA Business Associate Agreement. Justification: HIPAA compliance (NFR-013) eliminates public OpenAI API due to lack of BAA. Azure OpenAI provides GPT-4 with enterprise SLA, regional deployment options, and content filtering (AIR-S04). Alternative HIPAA-compliant providers introduce vendor lock-in risk or reduced model quality. Azure integration with .NET backend via official SDK reduces integration friction.

## Technology Stack
| Layer | Technology | Version | Justification (NFR/DR/AIR) |
|-------|------------|---------|----------------------------|
| Frontend | React | 18.x | NFR-015 (free Netlify/Vercel hosting), NFR-001 (2s response via SPA), BRD-approved stack |
| UI Components | Material-UI | 5.x | NFR-015 (OSS), accelerates UI development for patient/staff workflows |
| State Management | React Query + Zustand | 4.x / 4.x | NFR-001 (caching reduces API calls), NFR-015 (OSS lightweight libraries) |
| Mobile | N/A | - | Phase 1 web-only, mobile deferred to future phases |
| Backend | .NET 8 | 8.0 LTS | NFR-014 (Windows Services/IIS support), NFR-015 (OSS), Azure OpenAI SDK integration |
| API Framework | ASP.NET Core Web API | 8.0 | NFR-001 (async I/O performance), NFR-004 (built-in auth middleware) |
| Database | PostgreSQL | 15.x | DR-016 (pgvector extension for RAG), NFR-015 (OSS), DR-011 (referential integrity) |
| Vector Extension | pgvector | 0.5.x | AIR-R02 (similarity search for RAG retrieval), DR-016 (consolidated infrastructure) |
| Caching | Upstash Redis | Cloud | NFR-019 (BRD-mandated), NFR-001 (session and query caching), NFR-015 (free tier) |
| AI/ML - LLM | Azure OpenAI GPT-4 | 2024-02-01 | AIR-001 (extraction), AIR-002 (conversational), NFR-013 (HIPAA BAA) |
| AI/ML - Embeddings | text-embedding-3-small | latest | AIR-R01 (document chunking), AIR-O01 (cost efficiency), AIR-R02 (retrieval) |
| AI/ML - Vector Store | pgvector (PostgreSQL) | 0.5.x | AIR-R02 (similarity search), NFR-015 (OSS), DR-016 (single database) |
| AI/ML - Gateway | Custom .NET Middleware | custom | AIR-O01 (token budget), AIR-S03 (audit), NFR-015 (OSS, no paid gateway) |
| ORM | Entity Framework Core | 8.0 | DR-011 (type-safe queries), DR-014 (migration tooling), NFR-015 (OSS) |
| Background Jobs | Hangfire | 1.8.x | NFR-015 (OSS), NFR-002 (async document processing), recurring reminder jobs |
| Testing - Unit | xUnit + Moq | 2.x / 4.x | NFR-015 (OSS), development quality assurance |
| Testing - Integration | Testcontainers | 3.x | NFR-015 (OSS), database integration testing with isolated containers |
| Testing - E2E | Playwright | 1.x | NFR-015 (OSS), cross-browser UI workflow validation |
| Infrastructure - Hosting | Vercel (dev) → IIS (prod) | - | NFR-014 (IIS production), NFR-015 (Vercel free tier for dev/staging) |
| Infrastructure - Containers | Docker | 24.x | NFR-015 (OSS), development environment consistency |
| Security - Auth | ASP.NET Core Identity | 8.0 | NFR-004 (RBAC), NFR-005 (session timeout), NFR-013 (HIPAA audit) |
| Security - Encryption | .NET Data Protection API | 8.0 | NFR-003 (AES-256 at rest), DR-015 (PHI column encryption) |
| Security - TLS | Let's Encrypt | - | NFR-003 (TLS 1.2+ in transit), NFR-015 (free certificates) |
| Deployment - CI/CD | GitHub Actions | - | NFR-015 (free for public repos), DR-014 (zero-downtime migrations) |
| Monitoring - APM | Azure Application Insights (free tier) | - | NFR-006 (uptime tracking), NFR-018 (operational metrics), AIR-Q05 (AI agreement tracking) |
| Monitoring - Logs | Serilog + Seq (local) / Azure Monitor (prod) | 3.x / - | NFR-007 (audit trail), AIR-S03 (AI prompt logging), NFR-015 (OSS Serilog) |
| Documentation - API | Swagger/OpenAPI | 6.x | NFR-015 (OSS), developer experience for frontend-backend integration |
| Documentation - Code | XML Comments + DocFX | - | NFR-015 (OSS), architecture and API reference generation |
**Note:** All technology choices traced to NFR/DR/AIR requirements. AI/ML stack included based on AIR-XXX requirements from spec.md AI-CANDIDATE and HYBRID tags.

### Alternative Technology Options
- **Angular vs React (Frontend)**: Angular considered for BRD-approved option set. Rejected due to heavier framework overhead, steeper learning curve for greenfield team, and weaker free hosting ecosystem compared to React's Netlify/Vercel native support.
- **Java vs .NET (Backend)**: Java Spring Boot considered for BRD-approved option set. Rejected because .NET provides superior native Windows Services/IIS deployment support (NFR-014 explicit requirement), better Azure OpenAI SDK for HIPAA compliance, and comparable OSS tooling quality.
- **Pinecone vs pgvector (Vector Store)**: Pinecone specialized vector database considered for RAG retrieval. Rejected due to paid pricing model violating NFR-015, additional infrastructure complexity, and pgvector's sufficient performance for phase-1 scale with benefit of consolidated database operations.
- **LiteLLM/Portkey vs Custom (AI Gateway)**: Third-party AI gateways considered for prompt routing and observability. Rejected because both introduce paid dependencies at scale violating NFR-015, and custom lightweight middleware satisfies token budgeting (AIR-O01), audit logging (AIR-S03), and circuit breaking (AIR-O02) requirements with full HIPAA compliance control.
- **AWS Bedrock vs Azure OpenAI (LLM Provider)**: AWS Bedrock considered for vendor diversity. Rejected due to weaker .NET SDK support, comparable HIPAA BAA requirements, and Azure's tighter integration with existing Microsoft healthcare ecosystem if future EHR integration needed.

### AI Component Stack
| Component | Technology | Purpose |
|-----------|------------|---------|
| Model Provider | Azure OpenAI GPT-4 Turbo | LLM inference for extraction, conversational intake, code suggestions with HIPAA BAA |
| Embedding Model | text-embedding-3-small | Generate 1536-dim vectors for document chunks with cost efficiency |
| Vector Store | pgvector (PostgreSQL extension) | Store and retrieve embeddings with cosine similarity search at <10ms p95 |
| AI Gateway | Custom .NET Middleware | Token budget enforcement, request/response audit logging, circuit breaker, prompt sanitization |
| Content Filter | Azure OpenAI Content Safety | Block harmful/inappropriate generated content before user display (AIR-S04) |
| Guardrails | Custom JSON Schema Validator | Enforce structured output format for extracted facts (AIR-Q04 98% schema validity) |

### Technology Decision
| Metric (from NFR/DR/AIR) | React + .NET + pgvector | Angular + Java + Pinecone | Rationale |
|--------------------------|-------------------------|---------------------------|-----------|
| Windows/IIS Deployment (NFR-014) | Native .NET support | Java requires Tomcat | .NET wins: built-in IIS hosting |
| Free Hosting (NFR-015) | Vercel React native | Angular works but heavier | React wins: better free tier fit |
| HIPAA AI Integration (NFR-013, AIR-XXX) | Azure OpenAI .NET SDK | AWS Bedrock Java SDK | Tie: both have BAA, .NET SDK more mature |
| Infrastructure Cost (NFR-015) | pgvector OSS, no vector DB | Pinecone paid at scale | React/.NET wins: all-OSS stack |
| Team Productivity (NFR-002) | React ecosystem larger | Angular more opinionated | React wins: greenfield team flexibility |
| **Winner** | **React + .NET + pgvector** | - | 4 metric wins, 1 tie, 0 losses |

## Technical Requirements
- TR-001: System MUST use React 18 for frontend SPA development to leverage free Netlify/Vercel hosting ecosystem and satisfy NFR-015
- TR-002: System MUST use .NET 8 LTS for backend API to support native Windows Services/IIS deployment per NFR-014 and Azure OpenAI SDK integration
- TR-003: System MUST use PostgreSQL 15+ with pgvector extension as primary database to consolidate relational and vector storage per DR-016 and NFR-015
- TR-004: System MUST use Upstash Redis for session caching and query result caching to satisfy BRD infrastructure mandate and NFR-001 performance targets
- TR-005: System MUST implement modular monolith architecture with patient-access and clinical-intelligence bounded contexts using layered pattern within each module
- TR-006: System MUST use Azure OpenAI Service with signed HIPAA BAA for all AI model inference to satisfy NFR-013 compliance requirements
- TR-007: System MUST implement custom AI gateway middleware for token budget enforcement (AIR-O01), audit logging (AIR-S03), and circuit breaking (AIR-O02) without third-party paid dependencies
- TR-008: System MUST use Entity Framework Core 8 with code-first migrations for database schema management to support zero-downtime deployments per DR-014
- TR-009: System MUST use Hangfire for background job processing (document extraction, reminder sending) to satisfy NFR-015 OSS requirement and NFR-002 performance targets
- TR-010: System MUST use ASP.NET Core Identity for authentication and role-based authorization to satisfy NFR-004 RBAC and NFR-005 session timeout requirements
- TR-011: System MUST implement RESTful API design with OpenAPI/Swagger documentation to enable clear frontend-backend contract and satisfy developer experience needs
- TR-012: System MUST integrate Google Calendar API and Microsoft Graph Outlook Calendar API using OAuth 2.0 for free calendar synchronization per spec FR-007
- TR-013: System MUST integrate Twilio Programmable SMS (free tier) and SendGrid Email API (free tier) for multi-channel reminder delivery per NFR-015
- TR-014: System MUST use PDFSharp or similar OSS library for PDF appointment confirmation generation per spec FR-007 and NFR-015
- TR-015: System MUST implement pgvector similarity search with cosine distance metric for RAG retrieval per AIR-R02 and AIR-R03
- TR-016: System MUST use GitHub Actions for CI/CD pipeline with automated testing, security scanning, and zero-downtime deployment to IIS per DR-014
- TR-017: System MUST use Azure Application Insights (free tier) or OSS alternative for application performance monitoring and operational metrics tracking per NFR-006 and NFR-018
- TR-018: System MUST use Serilog for structured logging with sinks to Seq (local development) and Azure Monitor (production) for audit trail per NFR-007
- TR-019: System MUST implement health check endpoints for uptime monitoring and automated failover to satisfy NFR-006 99.9% availability target
- TR-020: System MUST use Playwright for end-to-end testing of critical patient booking and staff clinical review workflows per NFR-015
- TR-021: System MUST implement database connection pooling with minimum 10, maximum 100 connections per instance to balance resource usage and concurrency per NFR-008
- TR-022: System MUST use .NET Data Protection API for PHI column-level encryption to satisfy NFR-003 and DR-015 encryption requirements
- TR-023: System MUST implement retry logic with exponential backoff for transient failures in external API calls (calendars, SMS, email) per NFR-016
- TR-024: System MUST use Docker containers for development environment consistency but deploy directly to IIS in production per NFR-014
- TR-025: System MUST implement feature flags using configuration-based toggles to enable gradual AI feature rollout and A/B testing of agreement rates per AIR-Q05

## Technical Constraints & Assumptions
- **Constraint 1**: Hosting must use free or open-source platforms in phase 1 (Netlify, Vercel, GitHub Codespaces class) with mandatory migration path to Windows Services or IIS for production deployment
- **Constraint 2**: Technology stack selection must stay within BRD-approved options: React or Angular frontend, .NET or Java backend, PostgreSQL or SQL Server database
- **Constraint 3**: Azure OpenAI Service requires signed HIPAA Business Associate Agreement which may have provisioning lead time and compliance audit requirements
- **Constraint 4**: Upstash Redis usage is mandatory per BRD infrastructure specification for caching layer
- **Constraint 5**: All auxiliary processing, background jobs, and utility tooling must use strictly free and open-source technology stacks (no paid SaaS beyond AI model provider)
- **Constraint 6**: System must support deployment to Windows Services or IIS which constrains containerization strategy to development-only use
- **Constraint 7**: pgvector extension performance limits to ~1M vectors before query latency degrades, sufficient for phase-1 scale but may require migration to specialized vector DB in future
- **Constraint 8**: Free tiers of Twilio SMS and SendGrid Email have monthly volume caps requiring upgrade or alternative providers if reminder volume exceeds limits
- **Constraint 9**: Azure OpenAI has regional availability constraints and may have rate limits requiring request queuing or fallback strategies
- **Assumption 1**: Development team has .NET and React expertise or can acquire it within project timeline
- **Assumption 2**: Medical practice deployment sites have Windows Server infrastructure available for IIS hosting in production
- **Assumption 3**: Phase-1 scale supports hundreds of concurrent users per practice, not thousands, allowing single monolith deployment without immediate need for horizontal scaling
- **Assumption 4**: Azure OpenAI Content Safety filters are sufficient for harmful content blocking without additional third-party moderation tools
- **Assumption 5**: PostgreSQL database single instance can handle combined transactional and vector workload for phase-1 traffic
- **Assumption 6**: HIPAA compliance audit will accept Azure OpenAI Service with BAA as acceptable safeguard for PHI in AI prompts
- **Assumption 7**: Google Calendar and Outlook Calendar free APIs support sufficient request volume for appointment synchronization without enterprise licensing
- **Assumption 8**: Vercel or Netlify free tier supports development and staging deployments with acceptable build time and bandwidth limits
- **Assumption 9**: Text chunking at 512 tokens with 25% overlap provides sufficient context preservation for medical document extraction accuracy
- **Assumption 10**: Two-minute chart prep target is achievable with optimized RAG retrieval and parallel fact extraction processing

## Development Workflow

1. **Environment Setup**: Configure PostgreSQL 15 with pgvector extension, Upstash Redis cloud instance, .NET 8 SDK, and React 18 development environment. Set up Azure OpenAI Service with HIPAA BAA provisioning and API key management. Configure local development using Docker Compose for database and Redis services.

2. **Shared Kernel Implementation**: Build cross-cutting infrastructure layer including ASP.NET Core Identity for RBAC authentication, custom authorization policies for patient/staff/admin separation, audit logging interceptor for EF Core, HIPAA-compliant structured logging configuration with Serilog, session timeout middleware, and .NET Data Protection API setup for PHI encryption.

3. **Patient Access Module (Priority 1)**: Implement deterministic booking workflows first for highest business value. Build appointment availability search API, booking transaction with optimistic concurrency, preferred slot swap watchlist background job, no-show risk scoring calculator, intake form (manual mode initially), Google/Outlook calendar integration with OAuth flow, reminder scheduling with Hangfire, and PDF confirmation generation. All features fully deterministic, enabling early delivery and no-show reduction impact.

4. **Clinical Intelligence Module - Infrastructure (Priority 2)**: Set up AI pipeline foundation including Azure OpenAI client configuration with HIPAA BAA validation, custom AI gateway middleware for token budgeting and circuit breaking, pgvector schema for embeddings storage, document chunking service with 512-token segments and 25% overlap, embedding generation workflow using text-embedding-3-small, and RAG retrieval engine with cosine similarity search and re-ranking logic.

5. **Clinical Intelligence Module - Extraction (Priority 3)**: Implement AI-assisted document processing including clinical document upload API with virus scanning, async extraction job using Hangfire, RAG-based fact extraction with GPT-4 prompts, source citation anchor generation with character offsets, confidence scoring and automatic fallback to manual review at <70% threshold, ExtractedFact persistence with document foreign keys, and 360-degree view aggregation with conflict detection rules for contradictory medications or history.

6. **Clinical Intelligence Module - Verification UI (Priority 4)**: Build staff review interfaces including 360-degree patient view dashboard with source highlighting, conflict resolution workflow with accept/reject/manual-override options, ICD-10 and CPT code suggestion display with supporting evidence breadcrumbs, staff confirmation actions with audit trail capture, and real-time AI agreement rate metrics displayed to clinical reviewers for transparency.

7. **AI Conversational Intake Enhancement (Priority 5)**: Add hybrid conversational intake mode to existing manual forms including GPT-4 conversational prompt chains for intake questions, state management for mixed conversational-manual switching, response validation and sanitization (AIR-S01 PII redaction), answer persistence with mode indicator, and fallback to manual mode on conversation timeout or user preference.

8. **Security Hardening**: Conduct comprehensive security review including PHI encryption configuration validation, HIPAA audit log completeness verification, penetration testing of authentication and authorization boundaries, Azure OpenAI prompt injection attack surface analysis, database access control list enforcement for multi-tenancy if applicable, TLS certificate configuration with Let's Encrypt, and content security policy headers.

9. **Observability & Monitoring Setup**: Implement production monitoring including Azure Application Insights integration for APM, custom metrics for AI agreement rate (NFR-009) and critical conflicts detected (NFR-018), uptime health check endpoints for 99.9% availability tracking (NFR-006), Serilog structured logging with AI prompt/response capture (AIR-S03), alerting rules for circuit breaker trips and error rate spikes, and operational dashboard for booking volumes and no-show outcomes.

10. **Performance Optimization**: Tune system for NFR-001 and NFR-002 targets including React lazy loading and code splitting for 2-second booking load time, Redis query result caching for appointment availability searches, database query optimization with explain plans, pgvector index tuning for <10ms similarity search, AI gateway request batching for efficiency, background job parallelization for document processing, and load testing to validate 99.9% uptime under concurrent user load per NFR-008.

11. **Deployment Pipeline**: Configure CI/CD for zero-downtime releases including GitHub Actions workflow for build/test/deploy, Entity Framework Core migration pipeline with blue-green database pattern, IIS deployment automation with web deploy packages, Vercel staging environment for pre-production validation, automated smoke tests post-deployment, and rollback procedure for failed deployments within 1 hour per AIR-O03.

12. **Documentation & Training**: Create operational runbooks including architecture design document (this artifact), API documentation with Swagger/OpenAPI, database schema diagrams with entity relationships, AI prompt engineering guide for future tuning, HIPAA compliance certification checklist, staff training materials for AI verification UI, admin user management procedures, and incident response playbook for availability and security events.

