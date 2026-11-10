---
name: review-feature
description: Independently review a TopDownBase feature or integration change against its requirements, actual code, and verification evidence. Use for requested feature QA or independent review.
---

# 기능 독립 검토

1. 사용자의 검토 범위, `Docs/TopDownBase_Initial_Game_Plan.md`, 관련 작업 기록과 실제 변경 내역을 확인한다. 구현 보고는 단서로 사용하고 코드와 검증 근거를 직접 살핀다.
2. 요구사항 누락, 기능 오류, 기존 동작 회귀, 외부 에셋 원본 변경 및 검증 공백을 영향과 발생 가능성에 맞춰 살핀다. 불확실한 추론은 확인된 사실과 구분한다.
3. 게임 코드는 기본적으로 읽기 전용으로 다룬다. 사용자가 수정을 요청한 경우에만 그 범위에서 수정하고, 필요한 경우 관련 작업 문서에 QA를 기록한다.
4. 발견 사항을 심각도 순으로 보고하고 각 항목에 발생 조건, 사용자에게 미치는 영향, 파일과 줄 등 확인 가능한 근거를 적는다. 필요하다면 수정 방향을 제안한다.
5. 실제 실행한 검증과 실행하지 못한 검증을 구분한다. 발견 사항이 없다면 그 사실과 검토 범위 및 남은 위험을 밝힌다.
