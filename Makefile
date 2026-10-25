BUILD_SLN=./jaytwo.Ergonomics.Pdf.sln
BUILD_DIRS=./src/jaytwo.Ergonomics.Pdf
BUILD_TEST_DIRS=./test/jaytwo.Pdf.Tests:./test/jaytwo.Pdf.NetStandard.Tests:./test/jaytwo.Ergonomics.Pdf.Tests
ENABLE_COMPOSE_NETWORK=false

NUGET_SOURCE_URL?=https://api.nuget.org/v3/index.json
NUGET_API_KEY?=__missing_api_key__

TOPDIR=${CURDIR}
BUILD_TEST_RESULTS_DIR=${TOPDIR}/out/testResults
BUILD_TEST_COVERAGE_DIR=${TOPDIR}/out/coverage
BUILD_PACKED_DIR=${TOPDIR}/out/packed
PYTHON?=python
QUIET_RUN=${PYTHON} tools/agents/quiet_run.py

# W-118: a make invocation that does not set DOCKER_TAG gets its own image and container names, from the time and a
# process ID, so overlapping Docker runs neither take each other's container nor retag each other's image. Jenkins
# sets DOCKER_TAG. docker-run and docker-fuzz-campaign remove a generated tag's images when they finish; a caller that
# sets DOCKER_TAG keeps them, to run them again from another make invocation.
ifeq ($(origin DOCKER_TAG),undefined)
DOCKER_RUN_ID:=$(shell echo "$$(date +'%Y%m%d%H%M%S')_$$$$")
DOCKER_TAG=$(call getDockerTag,$(BUILD_SLN))__${DOCKER_RUN_ID}
DOCKER_TAG_GENERATED=true
endif
DOCKER_BASE_TAG?=${DOCKER_TAG}__base
DOCKER_BUILDER_TAG?=${DOCKER_TAG}__builder
DOCKER_BUILDER_CONTAINER?=${DOCKER_BUILDER_TAG}
DOCKER_RUN_MAKE_TARGETS?=run
TESTERNET_COMPOSE_PROJECT=${DOCKER_TAG}__testernet
TESTERNET_COMPOSE_NETWORK=${TESTERNET_COMPOSE_PROJECT}_default
TIMESTAMP?=$(call getTimestamp)

default: clean deps build test pack-beta nuget-check

deps:
	dotnet tool restore

clean:
	find . -name bin | xargs --no-run-if-empty rm -vrf
	find . -name obj | xargs --no-run-if-empty rm -vrf
	rm -rf ${TOPDIR}/out

restore:
	dotnet restore . --verbosity minimal

build: restore
	dotnet build "${BUILD_SLN}"

test: unit-test

agents-check:
	${PYTHON} tools/agents/workflow.py check

agents-status:
	${PYTHON} tools/agents/workflow.py status

task-check:
	${PYTHON} tools/agents/workflow.py task-check

agents-test:
	${PYTHON} tools/agents/test_workflow.py
	${PYTHON} tools/agents/test_quiet_run.py
	${PYTHON} tools/agents/test_slowest_tests.py

# W-102: agent verification with the full output in out/logs/<name>.log and only the exit status, per-framework test
# counts, failing tests, build errors, fuzz result, and the log tail on failure on the console. Jenkins and the targets
# they wrap keep their output. TEST_FRAMEWORK and TEST_FILTER narrow quiet-test, which runs without coverage.
# TEST_FRAMEWORK selects the engine test project, because the netstandard2.0 lane targets net8.0 only (D-51);
# TEST_PROJECT=./test/jaytwo.Pdf.NetStandard.Tests runs the lane alone, and TEST_PROJECT=./test/jaytwo.Ergonomics.Pdf.Tests
# the Ergonomics tests.
TEST_PROJECT?=$(if ${TEST_FRAMEWORK},${MAIN_TEST_DIR},${BUILD_SLN})

# W-122 (D-52): LANE=fast, the per-item default, leaves out tests by what they touch and sets JAYTWO_LANE, so a helper
# that starts a tool, loads iText, or opens a corpus file refuses. It then lists its slowest tests, because large-data
# and sweep tests have no helper that can refuse them.
FAST_LANE_FILTER=Category!=Interop&Category!=External&Category!=Corpus&Category!=LargeData&Category!=Sweep
FAST_LANE_RESULTS_DIR=${TOPDIR}/out/testResults/fast-lane
QUIET_TEST_FILTER=$(if $(filter fast,${LANE}),$(if ${TEST_FILTER},(${TEST_FILTER})&)${FAST_LANE_FILTER},${TEST_FILTER})

quiet-build:
	${QUIET_RUN} --name build -- dotnet build "${BUILD_SLN}" --nologo $(if ${CONFIGURATION},-c ${CONFIGURATION})

quiet-test:
	$(if $(filter fast,${LANE}),rm -rf "${FAST_LANE_RESULTS_DIR}")
	$(if $(filter fast,${LANE}),JAYTWO_LANE=fast) ${QUIET_RUN} --name test --tests -- dotnet test "${TEST_PROJECT}" --nologo -p:CollectCoverage=false 		$(if ${TEST_FRAMEWORK},-f ${TEST_FRAMEWORK}) $(if ${QUIET_TEST_FILTER},--filter "${QUIET_TEST_FILTER}") 		$(if $(filter fast,${LANE}),--results-directory "${FAST_LANE_RESULTS_DIR}" --logger "trx;LogFilePrefix=fast")
	$(if $(filter fast,${LANE}),${PYTHON} tools/agents/slowest_tests.py "${FAST_LANE_RESULTS_DIR}")

quiet-docker-test:
	${QUIET_RUN} --name docker-test --tests -- $(MAKE) docker-builder docker-test

# W-79: the QUALIFICATION.md G7 workloads, each in its own Release process with coverage off, so the process-wide heap
# counter sees one workload. G7_FRAMEWORKS narrows the frameworks (net48 runs only on Windows). Each run's full output
# is out/logs/g7-<framework>-<workload>.log; the console gets the measurement lines.
G7_FRAMEWORKS?=net10.0 net8.0 net6.0 $(if $(filter Windows_NT,${OS}),net48)
G7_WORKLOADS=dense_gibibyte_pdf_is_inspected_edited_and_signed_within_budget \
	large_attachment_is_added_and_extracted_through_file_streams_within_budget \
	five_hundred_page_form_is_extracted_edited_and_signed_within_budget \
	thousand_page_form_is_flattened_within_budget \
	repeated_open_and_object_stream_reads_do_not_retain_memory \
	encrypted_xref_stream_gibibyte_pdf_opens_without_a_discovery_scan

g7-workloads:
	dotnet build "${BUILD_SLN}" --nologo -c Release
	mkdir -p out/logs
	status=0; for framework in ${G7_FRAMEWORKS}; do for workload in ${G7_WORKLOADS}; do \
		log="out/logs/g7-$$framework-$$workload.log"; \
		dotnet test "${MAIN_TEST_DIR}" --nologo --no-build -c Release -f $$framework -p:CollectCoverage=false \
			--filter "FullyQualifiedName=jaytwo.Pdf.Tests.LargeWorkloadTests.$$workload" \
			--logger "console;verbosity=detailed" > "$$log" 2>&1 && result=PASS || { result=FAIL; status=1; }; \
		echo "$$framework $$workload: $$result"; \
		grep -E 'peak heap growth|retained after warm-up|Assert\.' "$$log" | sed 's/^[[:space:]]*/  /'; \
	done; done; exit $$status

# W-111: the G7 workloads through this library and through iText, each test in its own Release process, so the heap
# counter and the peak working set see one workload. The comparison project is outside the solution, so CI never
# restores iText. FixtureSetup.prepare rewrites the fixtures first, in a process of its own, into JAYTWO_COMPARISON_DIR
# (default: a temp directory). Each run's full output is out/logs/compare-<test>.log; the console gets the table.
COMPARISON_PROJECT=./test/jaytwo.Ergonomics.Pdf.Comparison
COMPARISON_WORKLOADS=dense_inspect dense_metadata_edit dense_sign attachment_add attachment_extract form_extraction \
	form_metadata_edit form_bulk_fill form_sign flatten encrypted_inspect_user encrypted_inspect_owner
COMPARISON_TESTS=$(foreach workload,${COMPARISON_WORKLOADS},ErgonomicsWorkloads.${workload} ITextWorkloads.${workload}) \
	ITextWorkloads.dense_inspect_from_stream ITextWorkloads.dense_sign_in_memory

compare-itext:
	dotnet build "${COMPARISON_PROJECT}" --nologo -c Release
	mkdir -p out/logs
	status=0; for test in FixtureSetup.prepare ${COMPARISON_TESTS}; do \
		log="out/logs/compare-$$test.log"; \
		dotnet test "${COMPARISON_PROJECT}" --nologo --no-build -c Release \
			--filter "FullyQualifiedName=jaytwo.Ergonomics.Pdf.Comparison.$$test" \
			--logger "console;verbosity=detailed" > "$$log" 2>&1 && result=PASS || { result=FAIL; status=1; }; \
		echo "$$test: $$result"; \
	done; \
	${PYTHON} tools/comparison/report.py $(foreach test,${COMPARISON_TESTS},out/logs/compare-${test}.log) || status=1; \
	exit $$status

# W-123 (D-52): after an intended change to the bytes the library writes, rewrite the output-identity texts from net10.0
# and review their diff. Every other target must then agree with them.
output-identity-record:
	JAYTWO_RECORD_OUTPUT_IDENTITY=1 dotnet test "${MAIN_TEST_DIR}" --nologo -f net10.0 -p:CollectCoverage=false \
		--filter "FullyQualifiedName~OutputIdentityTests"

# W-123 (D-52): Interop tests run on INTEROP_FRAMEWORK only. The library appends the same revision on every target, and
# OutputIdentityTests checks that on each of them, so the other frameworks and the netstandard2.0 lane leave Interop
# out. One build, then one test run per framework in parallel, as a multi-targeted dotnet test would, each with its own
# filter, TRX file, and log. Clock tests assert on elapsed time, which the other hosts' load moves, so they run after
# the parallel runs, one host at a time, with their own coverage output. A failure in any run fails the target.
INTEROP_FRAMEWORK=net10.0
UNIT_TEST_FRAMEWORKS?=net10.0 net8.0 net6.0 $(if $(filter Windows_NT,${OS}),net48)
# W-130 (D-54): engine tests, which may use jaytwo.Pdf internals, are in MAIN_TEST_DIR and its netstandard2.0 lane;
# public workflow tests are in ERGONOMICS_TEST_DIR.
MAIN_TEST_DIR=./test/jaytwo.Pdf.Tests
ERGONOMICS_TEST_DIR=./test/jaytwo.Ergonomics.Pdf.Tests
UNIT_TEST_RUNS=$(foreach framework,${UNIT_TEST_FRAMEWORKS},${MAIN_TEST_DIR}@${framework} ${ERGONOMICS_TEST_DIR}@${framework}) \
	./test/jaytwo.Pdf.NetStandard.Tests@net8.0

unit-test:
	rm -rf "${BUILD_TEST_RESULTS_DIR}"
	rm -rf "${BUILD_TEST_COVERAGE_DIR}"
	dotnet build "${BUILD_SLN}"
	mkdir -p "${BUILD_TEST_RESULTS_DIR}"
	status=0; pids=; for run in ${UNIT_TEST_RUNS}; do \
		dir=$${run%@*}; framework=$${run#*@}; name=$$(basename $$dir)-$$framework; filter='Category!=Clock&Category!=Interop'; \
		if [ "$$dir" = "${MAIN_TEST_DIR}" ] && [ "$$framework" = "${INTEROP_FRAMEWORK}" ]; then filter='Category!=Clock'; fi; \
		mkdir -p "${BUILD_TEST_RESULTS_DIR}/tmp/$$name"; \
		TMPDIR="${BUILD_TEST_RESULTS_DIR}/tmp/$$name" dotnet test "$$dir" --no-build -f $$framework --filter "$$filter" \
			--results-directory "${BUILD_TEST_RESULTS_DIR}" --logger "trx;LogFileName=$$name.trx" \
			> "${BUILD_TEST_RESULTS_DIR}/$$name.log" 2>&1 & pids="$$pids $$!"; \
	done; \
	for pid in $$pids; do wait $$pid || status=1; done; \
	for run in ${UNIT_TEST_RUNS}; do \
		dir=$${run%@*}; framework=$${run#*@}; name=$$(basename $$dir)-$$framework-clock; \
		dotnet test "$$dir" --no-build -f $$framework --filter Category=Clock \
			-p:CoverletOutput="${BUILD_TEST_COVERAGE_DIR}/clock/$$name/" \
			--results-directory "${BUILD_TEST_RESULTS_DIR}" --logger "trx;LogFileName=$$name.trx" \
			> "${BUILD_TEST_RESULTS_DIR}/$$name.log" 2>&1 || status=1; \
	done; \
	for log in "${BUILD_TEST_RESULTS_DIR}"/*.log; do echo "== $$log"; cat "$$log"; done; \
	exit $$status
	dotnet tool run reportgenerator \
		"-reports:${BUILD_TEST_COVERAGE_DIR}/**/*.cobertura.xml" \
		"-targetdir:${BUILD_TEST_COVERAGE_DIR}/" \
		"-reportTypes:Cobertura"
	dotnet tool run reportgenerator \
		"-reports:${BUILD_TEST_COVERAGE_DIR}/**/*.cobertura.xml" \
		"-targetdir:${BUILD_TEST_COVERAGE_DIR}/html" \
		"-reportTypes:Html"
	$(MAKE) fuzz-smoke

# W-18 (legacy B-18): the fixed-seed fuzz smoke run (G8). Deterministic; a failure is a regression.
fuzz-smoke:
	dotnet run --project ./tools/fuzz/jaytwo.Ergonomics.Pdf.Fuzz.csproj -c Release -- smoke

# W-18 (legacy B-18): a bounded libFuzzer campaign in the fuzz image. FUZZ_TARGETS is TARGET:SECONDS pairs; CPU time is about
# FUZZ_WORKERS x SECONDS per target. D-23 caps a campaign at 24 aggregate CPU-hours.
FUZZ_WORKERS?=7
FUZZ_TARGETS?=open:60 edit:60 object:60 objstm:60 decode:60 der:60 tsr:60 xmp:60
docker-fuzz-campaign:
	docker build -t ${DOCKER_TAG}__fuzz . --target fuzz
	EXIT_CODE=0; docker run --rm -v "${TOPDIR}/out/fuzz:/out" \
		-e COMMIT=$$(git rev-parse HEAD) -e DIRTY=$$(git status --porcelain | grep -q . && echo yes || echo no) \
		${DOCKER_TAG}__fuzz sh tools/fuzz/run-campaign.sh /out ${FUZZ_WORKERS} ${FUZZ_TARGETS} || EXIT_CODE=$$?; \
	$(if ${DOCKER_TAG_GENERATED},docker rmi ${DOCKER_TAG}__fuzz > /dev/null || true;) \
	exit $$EXIT_CODE

pack:
	rm -rf "${BUILD_PACKED_DIR}";
	for dir in $$(echo "${BUILD_DIRS}" | tr ':' '\n'); do \
		[ -n "$$dir" ] \
			&& cd "${TOPDIR}" \
			&& cd "$$dir" \
			&& dotnet pack -o "${BUILD_PACKED_DIR}" -p:ContinuousIntegrationBuild=true ${PACK_ARG}; \
	done

pack-beta: PACK_ARG=--version-suffix beta-${TIMESTAMP}
pack-beta: pack

nuget-check:
	PACKED_NUPKG_FILES="$(call getNupkgFiles)"; \
	if [ -z "$$PACKED_NUPKG_FILES" ]; then \
		echo "No packages found to check." >&2; exit 1; \
	fi; \
	for nupkg in $$PACKED_NUPKG_FILES; do \
		if [ -n "$$nupkg" ]; then \
			dotnet tool run nugetcheck \
				"$$nupkg" \
				-gte "$$nupkg" \
				--same-major \
				--fail-on-match \
			&& echo "NuGetCheck OK: $$(basename $$nupkg)" \
			|| { echo "NuGetCheck FAILED: $$(basename $$nupkg)" >&2; exit 1; }; \
		fi; \
	done

# D-54: jaytwo.Pdf is not packable while the release track is suspended, so the jaytwo.Ergonomics.Pdf package depends
# on a package that does not exist. pack and nuget-check still run; nothing is pushed until the owner adopts packaging.
nuget-push: nuget-check
nuget-push:
	@echo "nuget-push is disabled: the release track is suspended and jaytwo.Pdf is not packaged (D-54)." >&2; exit 1
	PACKED_NUPKG_FILES="$(call getNupkgFiles)"; \
	for nupkg in $$PACKED_NUPKG_FILES; do \
		if [ -n "$$nupkg" ]; then \
			dotnet nuget push \
				"$$nupkg" \
				--source "${NUGET_SOURCE_URL}" \
				--api-key "$$NUGET_API_KEY"; \
		fi; \
	done

localdev:
	@echo "localdev is disabled. Set ENABLE_COMPOSE_NETWORK=true to enable."

localdev-logs:
	@echo "localdev is disabled. Set ENABLE_COMPOSE_NETWORK=true to enable."

localdev-clean:
	@echo "localdev is disabled. Set ENABLE_COMPOSE_NETWORK=true to enable."

testernet-up:
	@echo "testernet is disabled. Set ENABLE_COMPOSE_NETWORK=true to enable."

testernet-run:
	@echo "testernet is disabled. Set ENABLE_COMPOSE_NETWORK=true to enable."

testernet-clean:
	@echo "testernet is disabled. Set ENABLE_COMPOSE_NETWORK=true to enable."

testernet-down: testernet-clean

docker-builder:
	docker build -t ${DOCKER_BASE_TAG} . --target base --pull
	docker build -t ${DOCKER_BUILDER_TAG} . --target builder --pull

docker: docker-builder
	docker build -t ${DOCKER_TAG} . --pull

# W-118: the container is created first, so a failed create, such as a name conflict, fails the target before it copies
# or removes a container that is not its own.
docker-run:
	docker create --name ${DOCKER_BUILDER_CONTAINER} ${DOCKER_BUILDER_TAG} make ${DOCKER_RUN_MAKE_TARGETS}
	EXIT_CODE=0; docker start --attach ${DOCKER_BUILDER_CONTAINER} || EXIT_CODE=$$?; \
	docker cp ${DOCKER_BUILDER_CONTAINER}:build/out ./ || echo "Could not copy out of builder container: Container not found: ${DOCKER_BUILDER_CONTAINER}"; \
	docker rm ${DOCKER_BUILDER_CONTAINER} && echo "Container removed: ${DOCKER_BUILDER_CONTAINER}" || echo "Container not found: ${DOCKER_BUILDER_CONTAINER}"; \
	$(if ${DOCKER_TAG_GENERATED},docker rmi ${DOCKER_BUILDER_TAG} ${DOCKER_BASE_TAG} > /dev/null || true;) \
	exit $$EXIT_CODE

docker-copy-from-builder-output:
	docker cp ${DOCKER_BUILDER_CONTAINER}:build/out ./ || echo "Could not copy out of builder container: Container not found: ${DOCKER_BUILDER_CONTAINER}"

docker-test: DOCKER_RUN_MAKE_TARGETS=test
docker-test: docker-run

docker-pack: DOCKER_RUN_MAKE_TARGETS=pack
docker-pack: docker-run

docker-pack-beta: DOCKER_RUN_MAKE_TARGETS=pack-beta
docker-pack-beta: docker-run

docker-clean:
	docker rm ${DOCKER_BUILDER_CONTAINER} && echo "Container removed: ${DOCKER_BUILDER_CONTAINER}" || echo  "Nothing to clean up for: ${DOCKER_BUILDER_CONTAINER}. Skipping."
	docker rmi ${DOCKER_BUILDER_TAG} && echo "Image removed: ${DOCKER_BUILDER_TAG}" || echo "Nothing to clean up for: ${DOCKER_BUILDER_TAG}. Skipping."
	docker rmi ${DOCKER_TAG} && echo "Image removed: ${DOCKER_TAG}" || echo "Nothing to clean up for: ${DOCKER_TAG}. Skipping."

define getDockerTag
$(shell echo '$(basename $(1))' | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]/_/g' | sed 's/^_*//')
endef

define getTimestamp
$(shell date +'%Y%m%d%H%M%S')
endef

define getNupkgFiles
$(shell ls -1 ${BUILD_PACKED_DIR}/*.nupkg)
endef
