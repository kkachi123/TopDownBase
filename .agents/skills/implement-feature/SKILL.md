---
name: implement-feature
description: Implement and verify a feature in the TopDownBase Unity project, investigating existing TopDown Engine capabilities and explaining the result for the developer. Use for feature implementation or feature-specific fixes in this project.
---

# 기능 구현

기능 목표와 완료 조건을 사용자의 요청과 `Docs/TopDownBase_Initial_Game_Plan.md`에서 확인한다. 관련 작업 기록이 있다면 함께 읽고, 미결정 사항은 확정된 요구로 취급하지 않는다.

1. 관련 프로젝트 코드와 TopDown Engine의 실제 구현을 조사해 재사용·확장 가능성을 확인한다. 지원 여부가 불확실하면 추측하지 말고 근거와 미확인 사항을 구분한다. 기능 착수 시 `Docs/Features/<분야명>/<기능명>/Process.md`를 작성하고, 분야별 `Process.md`와 루트 `PROCESS.md`에 연결한다. 작성 기준은 `Docs/TopDownBase_Plan_Writing_Guide.md`를 따른다.
2. 목적, 재사용 후보, 예상 변경 범위와 검증 방법을 간단히 알린다. 이미 승인된 요청 범위에서는 진행하고, 추가 권한이 필요하거나 결정이 불명확할 때만 확인한다.
3. 가장 작은 실용적인 변경을 구현한다. 외부 에셋 원본 코드·데모 씬·프리팹은 수정하지 않고 프로젝트 소유 코드, 복제본 또는 Variant로 확장한다. 관련 전문 스킬이 있으면 해당 지침도 따른다.
4. 변경에 적합한 검증을 수행한다. 코드 검토, 자동 테스트, Unity 에디터 실행과 실제 플레이 테스트를 구분하고, 수행하지 못한 항목은 이유와 함께 남긴다.
5. 변경 이유, 핵심 코드 흐름, 실제 검증 결과와 개발자가 직접 확인할 방법을 설명한다. 단계나 검증 상태가 바뀌면 기능별 과정과 관련 분야·전체 요약을 갱신한다. 여러 채팅이나 독립 검토로 인계할 필요가 있으면 기능 폴더의 `Handoff.md`에 목표, 결정, 변경, 검증과 미해결 항목을 기록한다. 단순한 일회성 수정은 기존 기능 문서에 필요한 내용만 반영한다.
6. 기능 작업 결과 해당 분야 전체의 완료 조건이 충족되면 `Docs/TopDownBase_Multi_Agent_Workflow.md`의 분야별 완료와 Git 커밋 절차를 따른다. 개별 기능 완료와 분야 전체 완료는 구분한다.
